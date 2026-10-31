Imports System.Collections.Concurrent
Imports System.Runtime.InteropServices
Imports System.Text.RegularExpressions
Imports OpenTK.Graphics.OpenGL4

''' <summary>OPENGL 4.3 STRICT (user decision 12-oct-2026, ola GL43: nothing above core OpenGL 4.3, not even as an extension). The one
''' place that writes in core 4.3 what the frame asked from 4.5: the direct state access entry points (ARB_direct_state_access, core
''' 4.5) as bind-to-edit with the previous binding restored, and glClipControl (ARB_clip_control, core 4.5) as the clip-space
''' conversion every last vertex stage applies (ClipGlsl) plus the facing it implies (FrontFace). The laws do not change: the D3D
''' clip position of EngineCameraLaw and of the transcribed VS (depth z in [0, w]; NDC y = +1 at render-target row 0 under
''' UPPER_LEFT) is the same number; only the way GL 4.3 is told changes.
''' <para>Bind-to-edit: every call binds the object to the target it names, does the core 4.3 call, and binds back what was bound
''' before (texture: the active unit's binding of that target; framebuffer: the draw / read binding; buffer: the copy-read binding).
''' The DSA call left the bindings untouched; so does this. The texture TARGET is the caller's: GL 4.3 has no query of a texture
''' name's target (TEXTURE_TARGET is a 4.5 query), and the callers know it.</para>
''' <para>THE TWO EXTENSIONS ADMITTED (user decision 13-oct-2026), each by a closed list the ParityGate gate gl43-api checks:
''' (1) EXT_texture_compression_s3tc / EXT_texture_sRGB's S3TC formats (BC1 / BC2 / BC3, never core GL): the eight tokens
''' 0x83F0..0x83F3 and 0x8C4C..0x8C4F, only in DirectXDDSLoader.vb (the DDS upload); (2) EXT_texture_filter_anisotropic (core
''' only in 4.6): the two tokens 0x84FE / 0x84FF, only in this module (MaxAnisotropy, TexAnisotropy, SamplerAnisotropy), which
''' the DDS upload, the engine sampler table and the FO4 environment cube sampler call. Nothing else above core 4.3.</para>
''' <para>Clip control: one 16-byte uniform buffer per GL context at binding ClipBinding holds the origin's sign; the shaders read
''' it through ClipGlsl. The state is per context (GL state is), kept by the context's WGL handle (InitContext / ReleaseContext).
''' </para>
''' <para>WHAT 4.3 COSTS IN DEPTH, ACCEPTED AS MEASURED (user decision 13-oct-2026; a published measurement, no gate tolerance).
''' glClipControl(ZERO_TO_ONE) stored the D3D z/w; 4.3 has only NEGATIVE_ONE_TO_ONE, so gl43ClipToGl writes z' = 2z - w and
''' the window transform gives back (z'/w + 1)/2 = z/w with float32 rounding in between. Measured by
''' D:\WinTmp\rc\gl43\work\depth_ab3.py (output depth_ab3.txt; 1024x768 non-overlapping tiles, EngineCameraLaw.Perspective
''' n 15 f 353840, D24, 442099 interior pixels), emulated minus native code: -2 in 9 px, -1 in 56450, 0 in 340215, +1 in
''' 45425. The 9 px at -2 are all next to the far plane (codes >= 2^24 - 8), and there the emulated code is the CLOSER one to the
''' exact code (the D3D depth of the triangle interpolated in double): near far, native - exact reaches 4, emulated - exact 3
''' (e.g. pixel (817,257): exact 16777208, native 16777211, emulated 16777209). Over all pixels both sides sit at +1 / +2 from
''' exact in most pixels (native 42.9 % / 35.3 %, emulated 45.0 % / 34.7 %). Folding 2z - w into the projection matrix was
''' measured too and discarded (up to -159 codes from native next to the near plane).</para>
''' <para>WHAT 4.3 COSTS IN DERIVATIVES (published measurement). The transcribed asm says deriv_rtx_coarse / deriv_rty_coarse
''' (the quad's top-left pair for every lane); dFdxCoarse / dFdyCoarse are GLSL 4.50 (ARB_derivative_control), and GLSL 4.30's
''' dFdx / dFdy leave the method to the implementation (8.13.1: forward or backward differencing, dx &lt;= 1; condition 3, 'may
''' vary by window coordinates'). Measured in the RawAO program itself (ShadowGate --sse-sao-law, rev-G1, NVIDIA 595.71): the
''' driver differences FINE, every lane on its own row / column pair (37x25: 444 pixels fine only, 481 where fine and coarse
''' coincide, 0 coarse only, 0 outside 8.13.1). Effect on the frame (ola GL43 v2 PNG battery against the 4.5 build, 648000 px):
''' sse_shadows_off 6009 px differ, at most 5 / 255; sse_shadows_on 5836 px, at most 3 / 255 (the SSE SAO smoothing of
''' 16012:126-138); the FO4 deferred lights (deriv of the pixel position, linear across a quad) do not change.
''' The derivative hint (GL_FRAGMENT_SHADER_DERIVATIVE_HINT) is NOT set (user decision 13-oct-2026): the derivative is the
''' driver's. Measured as data: FASTEST, NICEST and DONT_CARE give the same map (fine) on this driver.</para>
''' <para>A MEASURED GAP, declared, not covered by any margin (ShadowGate --sse-sao-law). The gates compare a byte written by
''' GL with the oracle under GL's own conversion law (Tools\ShadowGate\GlFixedPointLaw.vb: floor or ceil of the exact value,
''' GL 4.3 2.3.4.2 / 17.3.10, GL 4.6 2.3.5.2), and with the GPU's own float read by a probe. The v3.3 gap of the SSE SAO RawAO
''' (|GPU float - oracle| x 255 up to 0.1053) was bisected with probes of the program's own intermediates (cz, the sample
''' coordinate and level, each pyramid sample, each term, the sum, A0) to two GPU laws, now MEASURED and modelled by the oracle:
''' sin / cos (GLSL 4.30 4.7.1: undefined precision - the GPU's own, measured in the program, for every sample) and bilinear
''' filtering (coordinate truncated to 2^-21, weight rounded to 2^-8 half up, corner w11 = round(wx wy 2^8) / 2^8 and the
''' other three by difference; a UNORM8 texture's filtered result - the RawAO normals are RGBA8 - rounded to a multiple of
''' 1 / 65535 (rev-G17: the RGBA8 impulses, D:\WinTmp\rc\gl43\work\u8law.py and the gate's fit); 0 off the law on every pyramid
''' level of both windows, 2D, impulse and ramp textures, R32F and UNORM8). What
''' remains is the GPU's float32 arithmetic against the double oracle: max 0.0000 x 255 over the 925 pixels of 37x25, max
''' 0.0007 over the 775 of 31x25 (N = pixels with an integer between oracle x 255 and GPU x 255, published by the gate).</para>
''' <para>A MEASURED GAP of the storage law, declared. The engine's G-buffer literals are float32 immediates, not k / 255: three
''' written straight into the UNORM8 RT 0x1E (o3.w, the material / skin id the deferred pass reads) land just above an
''' integer: 0x3B8080C4 x 255 = 1.0000080, 0x3C8080C4 x 255 = 4.0000321, 0x3CA0A0F5 x 255 = 5.0000401 (the skin id). GL 4.3
''' leaves the code open between floor and ceil (2.3.4.2 'cast', 17.3.10); D3D11.3 rounds to nearest (1, 4, 5). Measured on
''' this NVIDIA (ShadowGate --fo4-gbuffer-law census): 1 and 5 stored as 1 and 5 (nearest) in every compared record
''' (1.0000080: rec2652..2655, 2727..2729, 2993..2997, 3049..3052; 5.0000401: rec2733..2738, 2804..2806, 2809, 2812, 2813,
''' 2820, 2825..2827, 2837, 2893, 2909, 3015); 4.0000321 (rec2796, 2797, 3024, 3025, 3057 in the transcription) is not reached
''' by a compared record, so not measured. A driver that stores the ceil gives the deferred pass another material id. Static
''' census with data flow (literals, FO4_L_ macros, vecN of constants and mov copies; D:\WinTmp\rc\gl43\work\census_lit2.py)
''' over every app program (the four transcriptions and every GLSL text of FO4_Base_Library): these three and no other
''' constant output with a non-integer stored value; the gate's dynamic census (Val.Literal over the 433 records) lists the
''' same two of them it reaches.</para>
''' <para>THE ORDER OF THE OPERATIONS IS NOT PINNED, declared (ola 2b rev-B7, decided by the reviewer 13-oct-2026): no GLSL text of
''' the app uses the precise qualifier, and without it GLSL 4.30 4.9 does not guarantee that an expression is evaluated in the
''' order and form it is written (one fma for a * b + c or two roundings, reassociation). The bounds the gates' oracle propagates
''' (Tools\ShadowGate\Fo4GBufferBytecode.vb OpErr) suppose the written order, so a red of a gate from that cause is a gap of this
''' law, not a defect of the transcription. Measured once: an exact float32 result claimed for a mad of the oracle put 20 PS of
''' --fo4-gbuffer-law outside the bound (rec2805 asm 69 / 75); that the compiler evaluated another order is the inference.
''' gates-v6 (rev-E18, decided 15-oct-2026 on a measured red: the b09 wind VS rec2328 / 2329 / 2342 / 2343, asm 256..257
''' r1.x * (u8 * 0.003922), the GPU 1 ULP away where the written-order bound gave 0.82 ULP): on an ASSOCIATIVE CHAIN - products or
''' sums of the same op, no other op between - the oracle's bound is that of ANY order of evaluation (Fo4GBufferBytecode.SetOpErr,
''' the one seat every gate's oracle uses): a product of n factors within (1 + u)^(n - 1) - 1 of the exact product, relative (Higham,
''' Accuracy and Stability of Numerical Algorithms, 2nd ed., Lemma 3.1), while no partial product leaves the normal range; a sum of
''' n terms within ((1 + u)^(n - 1) - 1) sum |x_i| (Higham 4.2), u = 2^-24. Out of those chains the written-order bound stands.</para>
''' ASCII only (GLSL).</summary>
Friend NotInheritable Class Gl43
    Private Sub New()
    End Sub

    ' =================================================================================================================================
    ' Textures (ARB_direct_state_access -> bind-to-edit)
    ' =================================================================================================================================

    ''' <summary>Every texture target of OpenGL 4.3 core (GL 4.3 8.1, BindTexture): what an unbind of a whole unit clears.</summary>
    Private Shared ReadOnly AllTextureTargets As TextureTarget() = {
        TextureTarget.Texture1D, TextureTarget.Texture2D, TextureTarget.Texture3D, TextureTarget.Texture1DArray,
        TextureTarget.Texture2DArray, TextureTarget.TextureRectangle, TextureTarget.TextureCubeMap, TextureTarget.TextureCubeMapArray,
        TextureTarget.TextureBuffer, TextureTarget.Texture2DMultisample, TextureTarget.Texture2DMultisampleArray}

    ''' <summary>The binding query of a texture target (GL 4.3 table 23.15). GL_TEXTURE_BINDING_CUBE_MAP_ARRAY (0x900A, core 4.0) has
    ''' no member in OpenTK's GetPName.</summary>
    Private Shared Function BindingQuery(target As TextureTarget) As GetPName
        Select Case target
            Case TextureTarget.Texture1D : Return GetPName.TextureBinding1D
            Case TextureTarget.Texture2D : Return GetPName.TextureBinding2D
            Case TextureTarget.Texture3D : Return GetPName.TextureBinding3D
            Case TextureTarget.Texture1DArray : Return GetPName.TextureBinding1DArray
            Case TextureTarget.Texture2DArray : Return GetPName.TextureBinding2DArray
            Case TextureTarget.TextureRectangle : Return GetPName.TextureBindingRectangle
            Case TextureTarget.TextureCubeMap : Return GetPName.TextureBindingCubeMap
            Case TextureTarget.TextureCubeMapArray : Return CType(&H900A, GetPName)
            Case TextureTarget.TextureBuffer : Return GetPName.TextureBindingBuffer
            Case TextureTarget.Texture2DMultisample : Return GetPName.TextureBinding2DMultisample
            Case TextureTarget.Texture2DMultisampleArray : Return GetPName.TextureBinding2DMultisampleArray
            Case Else : Throw New ArgumentException($"Gl43: {target} is not a texture target", NameOf(target))
        End Select
    End Function

    ''' <summary>The target a texture is bound to for an image target: a cube face is an image of the TEXTURE_CUBE_MAP texture.</summary>
    Private Shared Function BindTargetOf(imageTarget As TextureTarget) As TextureTarget
        Select Case imageTarget
            Case TextureTarget.TextureCubeMapPositiveX, TextureTarget.TextureCubeMapNegativeX, TextureTarget.TextureCubeMapPositiveY,
                 TextureTarget.TextureCubeMapNegativeY, TextureTarget.TextureCubeMapPositiveZ, TextureTarget.TextureCubeMapNegativeZ
                Return TextureTarget.TextureCubeMap
            Case Else
                Return imageTarget
        End Select
    End Function

    ''' <summary>Runs <paramref name="body"/> with <paramref name="texture"/> bound to <paramref name="target"/> of the active unit,
    ''' then binds back the texture that was there.</summary>
    Private Shared Sub WithTexture(target As TextureTarget, texture As Integer, body As Action)
        Dim prev = GL.GetInteger(BindingQuery(target))
        GL.BindTexture(target, texture)
        Try
            body()
        Finally
            GL.BindTexture(target, prev)
        End Try
    End Sub

    ''' <summary>glCreateTextures(target, 1): a new name, and the texture object of that target created by its first bind (GL 4.3
    ''' 8.1: "a new texture object is created when an unused name is bound").</summary>
    Friend Shared Function CreateTexture(target As TextureTarget) As Integer
        Dim t = GL.GenTexture()
        WithTexture(target, t, Sub()
                               End Sub)
        Return t
    End Function

    ''' <summary>glTextureStorage2D: TexStorage2D (core 4.2) on the bound texture.</summary>
    Friend Shared Sub TexStorage2D(texture As Integer, target As TextureTarget2d, levels As Integer, format As SizedInternalFormat, width As Integer, height As Integer)
        WithTexture(CType(target, TextureTarget), texture, Sub() GL.TexStorage2D(target, levels, format, width, height))
    End Sub

    ''' <summary>glTextureStorage3D: TexStorage3D (core 4.2) on the bound texture.</summary>
    Friend Shared Sub TexStorage3D(texture As Integer, target As TextureTarget3d, levels As Integer, format As SizedInternalFormat, width As Integer, height As Integer, depth As Integer)
        WithTexture(CType(target, TextureTarget), texture, Sub() GL.TexStorage3D(target, levels, format, width, height, depth))
    End Sub

    ''' <summary>glTextureSubImage2D: TexSubImage2D on <paramref name="imageTarget"/> (a cube face for a cube map).</summary>
    Friend Shared Sub TexSubImage2D(Of T As Structure)(texture As Integer, imageTarget As TextureTarget, level As Integer, x As Integer, y As Integer,
                                                width As Integer, height As Integer, format As PixelFormat, type As PixelType, data As T())
        WithTexture(BindTargetOf(imageTarget), texture, Sub() GL.TexSubImage2D(imageTarget, level, x, y, width, height, format, type, data))
    End Sub

    ''' <summary>glTextureSubImage3D: TexSubImage3D (a 3D, 2D-array or cube-map-array texture; z = layer-face for a cube map array).</summary>
    Friend Shared Sub TexSubImage3D(Of T As Structure)(texture As Integer, target As TextureTarget, level As Integer, x As Integer, y As Integer, z As Integer,
                                                width As Integer, height As Integer, depth As Integer, format As PixelFormat, type As PixelType, data As T())
        WithTexture(target, texture, Sub() GL.TexSubImage3D(target, level, x, y, z, width, height, depth, format, type, data))
    End Sub

    ''' <summary>glTextureParameteri.</summary>
    Friend Shared Sub TexParameter(texture As Integer, target As TextureTarget, pname As TextureParameterName, value As Integer)
        WithTexture(target, texture, Sub() GL.TexParameter(target, pname, value))
    End Sub

    ''' <summary>glGetTextureParameteriv.</summary>
    Friend Shared Sub GetTexParameter(texture As Integer, target As TextureTarget, pname As GetTextureParameter, ByRef value As Integer)
        Dim v As Integer
        WithTexture(target, texture, Sub() GL.GetTexParameter(target, pname, v))
        value = v
    End Sub

    ''' <summary>The image a level query of <paramref name="target"/> reads: a cube map's +X face (GL 4.3 8.11: GetTexLevelParameter
    ''' takes a face, not TEXTURE_CUBE_MAP; the six faces of a complete cube have the same size and format).</summary>
    Private Shared Function LevelQueryTarget(target As TextureTarget) As TextureTarget
        Return If(target = TextureTarget.TextureCubeMap, TextureTarget.TextureCubeMapPositiveX, target)
    End Function

    ''' <summary>glGetTextureLevelParameteriv.</summary>
    Friend Shared Sub GetTexLevelParameter(texture As Integer, target As TextureTarget, level As Integer, pname As GetTextureParameter, ByRef value As Integer)
        Dim v As Integer
        WithTexture(BindTargetOf(target), texture, Sub() GL.GetTexLevelParameter(LevelQueryTarget(target), level, pname, v))
        value = v
    End Sub

    ''' <summary>glCopyTextureSubImage2D: CopyTexSubImage2D from the bound read framebuffer.</summary>
    Friend Shared Sub CopyTexSubImage2D(texture As Integer, imageTarget As TextureTarget, level As Integer, xoffset As Integer, yoffset As Integer,
                                 x As Integer, y As Integer, width As Integer, height As Integer)
        WithTexture(BindTargetOf(imageTarget), texture, Sub() GL.CopyTexSubImage2D(imageTarget, level, xoffset, yoffset, x, y, width, height))
    End Sub

    ''' <summary>glGetTextureImage: GetTexImage of one image (2D, 3D, array or a cube face), with the DSA call's bound: the bytes
    ''' the pack writes (GL 4.3 8.4.4 / 18.2 pixel storage, PackedImageBytes) must fit in <paramref name="bufSize"/> and in
    ''' <paramref name="data"/>, else the INVALID_OPERATION of glGetTextureImage (GL 4.5 8.11) is an exception here and nothing is
    ''' written.</summary>
    Friend Shared Sub GetTexImage(Of T As Structure)(texture As Integer, imageTarget As TextureTarget, level As Integer, format As PixelFormat,
                                              type As PixelType, bufSize As Integer, data As T())
        Dim bindTarget = BindTargetOf(imageTarget)
        WithTexture(bindTarget, texture,
                    Sub()
                        Dim w, h, d As Integer
                        GL.GetTexLevelParameter(imageTarget, level, GetTextureParameter.TextureWidth, w)
                        GL.GetTexLevelParameter(imageTarget, level, GetTextureParameter.TextureHeight, h)
                        GL.GetTexLevelParameter(imageTarget, level, GetTextureParameter.TextureDepth, d)
                        Dim need = PackedImageBytes(w, h, d, format, type)
                        Dim have = CLng(data.Length) * Marshal.SizeOf(Of T)()
                        If need > bufSize OrElse need > have Then
                            Throw New InvalidOperationException($"Gl43.GetTexImage: {need} bytes to pack, bufSize {bufSize}, array {have}")
                        End If
                        GL.GetTexImage(imageTarget, level, format, type, data)
                    End Sub)
    End Sub

    ''' <summary>The bytes from the pointer to the end of the last pixel a pack of a w x h x d image writes, under the current
    ''' PACK_* state (GL 4.3 8.4.4.1 "Unpacking" read with the pack parameters, 18.2): a row of l = ROW_LENGTH or w groups of n
    ''' elements of s bytes is k = n l elements when s >= a, else (a / s) ceil(s n l / a) (a = ALIGNMENT); an image is IMAGE_HEIGHT
    ''' or h rows; SKIP_PIXELS / SKIP_ROWS / SKIP_IMAGES move the start. A packed type is one element of its size per group.</summary>
    Private Shared Function PackedImageBytes(w As Integer, h As Integer, d As Integer, format As PixelFormat, type As PixelType) As Long
        Dim n As Integer, s As Integer
        Select Case type
            Case PixelType.UnsignedByte, PixelType.Byte : s = 1
            Case PixelType.UnsignedShort, PixelType.Short, PixelType.HalfFloat : s = 2
            Case PixelType.UnsignedInt, PixelType.Int, PixelType.Float : s = 4
            Case PixelType.UnsignedInt248, PixelType.UnsignedInt10F11F11FRev, PixelType.UnsignedInt2101010Reversed, PixelType.UnsignedInt5999Rev
                s = 4 : n = 1
            Case PixelType.Float32UnsignedInt248Rev
                s = 8 : n = 1
            Case Else : Throw New NotSupportedException($"Gl43.GetTexImage: pixel type {type}")
        End Select
        If n = 0 Then
            Select Case format
                Case PixelFormat.Red, PixelFormat.Green, PixelFormat.Blue, PixelFormat.Alpha, PixelFormat.DepthComponent, PixelFormat.StencilIndex,
                     PixelFormat.RedInteger
                    n = 1
                Case PixelFormat.Rg, PixelFormat.RgInteger : n = 2
                Case PixelFormat.Rgb, PixelFormat.Bgr, PixelFormat.RgbInteger : n = 3
                Case PixelFormat.Rgba, PixelFormat.Bgra, PixelFormat.RgbaInteger : n = 4
                Case Else : Throw New NotSupportedException($"Gl43.GetTexImage: pixel format {format} with type {type}")
            End Select
        End If
        Dim a = GL.GetInteger(GetPName.PackAlignment)
        Dim rowLength = GL.GetInteger(GetPName.PackRowLength)
        Dim skipPixels = GL.GetInteger(GetPName.PackSkipPixels)
        Dim skipRows = GL.GetInteger(GetPName.PackSkipRows)
        Dim imageHeight = GL.GetInteger(GetPName.PackImageHeight)
        Dim skipImages = GL.GetInteger(GetPName.PackSkipImages)
        Dim l = If(rowLength > 0, rowLength, w)
        Dim k As Long = If(s >= a, CLng(n) * l, CLng(a \ s) * ((CLng(s) * n * l + a - 1) \ a))
        Dim rowBytes = k * s
        Dim rows = If(imageHeight > 0, imageHeight, h)
        Dim imageBytes = rowBytes * rows
        Return CLng(skipImages + d - 1) * imageBytes + CLng(skipRows + h - 1) * rowBytes + CLng(skipPixels + w) * n * s
    End Function

    ''' <summary>glBindTextureUnit: <paramref name="texture"/> on <paramref name="target"/> of unit <paramref name="unit"/>, the active
    ''' unit selector back as it was. Texture 0 unbinds every target of the unit, as glBindTextureUnit(unit, 0) does (GL 4.5 8.1).</summary>
    Friend Shared Sub BindTextureUnit(unit As Integer, target As TextureTarget, texture As Integer)
        Dim active = GL.GetInteger(GetPName.ActiveTexture)
        GL.ActiveTexture(CType(TextureUnit.Texture0 + unit, TextureUnit))
        If texture = 0 Then
            For Each t In AllTextureTargets : GL.BindTexture(t, 0) : Next
        Else
            GL.BindTexture(target, texture)
        End If
        GL.ActiveTexture(CType(active, TextureUnit))
    End Sub

    ''' <summary>glBindTextureUnit(unit, 0): every target of unit <paramref name="unit"/> unbound.</summary>
    Friend Shared Sub UnbindTextureUnit(unit As Integer)
        BindTextureUnit(unit, TextureTarget.Texture2D, 0)
    End Sub

    ' =================================================================================================================================
    ' Framebuffers (ARB_direct_state_access -> bind-to-edit on the draw / read binding)
    ' =================================================================================================================================

    Private Shared Sub WithDrawFramebuffer(fbo As Integer, body As Action)
        Dim prev = GL.GetInteger(GetPName.DrawFramebufferBinding)
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo)
        Try
            body()
        Finally
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, prev)
        End Try
    End Sub

    Private Shared Sub WithReadFramebuffer(fbo As Integer, body As Action)
        Dim prev = GL.GetInteger(GetPName.ReadFramebufferBinding)
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo)
        Try
            body()
        Finally
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, prev)
        End Try
    End Sub

    ''' <summary>glCreateFramebuffers(1): a new name, and the framebuffer object created by its first bind (GL 4.3 9.2).</summary>
    Friend Shared Function CreateFramebuffer() As Integer
        Dim f = GL.GenFramebuffer()
        WithDrawFramebuffer(f, Sub()
                               End Sub)
        Return f
    End Function

    ''' <summary>glNamedFramebufferTexture: FramebufferTexture (core 3.2).</summary>
    Friend Shared Sub FramebufferTexture(fbo As Integer, attachment As FramebufferAttachment, texture As Integer, level As Integer)
        WithDrawFramebuffer(fbo, Sub() GL.FramebufferTexture(FramebufferTarget.DrawFramebuffer, attachment, texture, level))
    End Sub

    ''' <summary>glNamedFramebufferRenderbuffer.</summary>
    Friend Shared Sub FramebufferRenderbuffer(fbo As Integer, attachment As FramebufferAttachment, rbTarget As RenderbufferTarget, renderbuffer As Integer)
        WithDrawFramebuffer(fbo, Sub() GL.FramebufferRenderbuffer(FramebufferTarget.DrawFramebuffer, attachment, rbTarget, renderbuffer))
    End Sub

    ''' <summary>glNamedFramebufferDrawBuffers.</summary>
    Friend Shared Sub DrawBuffers(fbo As Integer, n As Integer, bufs As DrawBuffersEnum())
        WithDrawFramebuffer(fbo, Sub() GL.DrawBuffers(n, bufs))
    End Sub

    ''' <summary>glNamedFramebufferDrawBuffer.</summary>
    Friend Shared Sub DrawBuffer(fbo As Integer, mode As DrawBufferMode)
        WithDrawFramebuffer(fbo, Sub() GL.DrawBuffer(mode))
    End Sub

    ''' <summary>glNamedFramebufferReadBuffer.</summary>
    Friend Shared Sub ReadBuffer(fbo As Integer, mode As ReadBufferMode)
        WithReadFramebuffer(fbo, Sub() GL.ReadBuffer(mode))
    End Sub

    ''' <summary>glCheckNamedFramebufferStatus: the status of <paramref name="fbo"/> bound to <paramref name="target"/>
    ''' (FRAMEBUFFER checks as DRAW_FRAMEBUFFER, GL 4.3 9.4.2).</summary>
    Friend Shared Function CheckFramebufferStatus(fbo As Integer, target As FramebufferTarget) As FramebufferStatus
        Dim r As FramebufferErrorCode
        If target = FramebufferTarget.ReadFramebuffer Then
            WithReadFramebuffer(fbo, Sub() r = GL.CheckFramebufferStatus(FramebufferTarget.ReadFramebuffer))
        Else
            WithDrawFramebuffer(fbo, Sub() r = GL.CheckFramebufferStatus(FramebufferTarget.DrawFramebuffer))
        End If
        Return CType(CInt(r), FramebufferStatus)
    End Function

    ''' <summary>glClearNamedFramebufferfv: ClearBuffer on the framebuffer.</summary>
    Friend Shared Sub ClearFramebuffer(fbo As Integer, buffer As ClearBuffer, drawbuffer As Integer, value As Single())
        WithDrawFramebuffer(fbo, Sub() GL.ClearBuffer(buffer, drawbuffer, value))
    End Sub

    ''' <summary>glClearNamedFramebufferiv.</summary>
    Friend Shared Sub ClearFramebuffer(fbo As Integer, buffer As ClearBuffer, drawbuffer As Integer, value As Integer())
        WithDrawFramebuffer(fbo, Sub() GL.ClearBuffer(buffer, drawbuffer, value))
    End Sub

    ''' <summary>glClearNamedFramebufferfi.</summary>
    Friend Shared Sub ClearFramebuffer(fbo As Integer, buffer As ClearBufferCombined, drawbuffer As Integer, depth As Single, stencil As Integer)
        WithDrawFramebuffer(fbo, Sub() GL.ClearBuffer(buffer, drawbuffer, depth, stencil))
    End Sub

    ''' <summary>glBlitNamedFramebuffer: BlitFramebuffer with both bindings set and put back.</summary>
    Friend Shared Sub BlitFramebuffer(readFbo As Integer, drawFbo As Integer, srcX0 As Integer, srcY0 As Integer, srcX1 As Integer, srcY1 As Integer,
                               dstX0 As Integer, dstY0 As Integer, dstX1 As Integer, dstY1 As Integer, mask As ClearBufferMask, filter As BlitFramebufferFilter)
        WithReadFramebuffer(readFbo,
                            Sub() WithDrawFramebuffer(drawFbo,
                                                      Sub() GL.BlitFramebuffer(srcX0, srcY0, srcX1, srcY1, dstX0, dstY0, dstX1, dstY1, mask, filter)))
    End Sub

    ' =================================================================================================================================
    ' Buffers
    ' =================================================================================================================================

    ''' <summary>glGetNamedBufferSubData: GetBufferSubData through COPY_READ_BUFFER (core 3.1; its binding query has the same value,
    ''' 0x8F36, GL 4.3 table 23.8), the binding put back.</summary>
    Friend Shared Sub GetBufferSubData(Of T As Structure)(buffer As Integer, offset As IntPtr, size As Integer, data As T())
        Dim prev = GL.GetInteger(CType(&H8F36, GetPName))
        GL.BindBuffer(BufferTarget.CopyReadBuffer, buffer)
        Try
            GL.GetBufferSubData(BufferTarget.CopyReadBuffer, offset, size, data)
        Finally
            GL.BindBuffer(BufferTarget.CopyReadBuffer, prev)
        End Try
    End Sub

    ' =================================================================================================================================
    ' Admitted extension 2: EXT_texture_filter_anisotropic (user decision 13-oct-2026). Its two tokens live only here.
    ' =================================================================================================================================

    ''' <summary>GL_TEXTURE_MAX_ANISOTROPY_EXT.</summary>
    Private Const TextureMaxAnisotropyExt As Integer = &H84FE
    ''' <summary>GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT.</summary>
    Private Const MaxTextureMaxAnisotropyExt As Integer = &H84FF

    ''' <summary>The context's GL_MAX_TEXTURE_MAX_ANISOTROPY_EXT; 0 without the extension (the query then leaves the value as it
    ''' is and queues INVALID_ENUM, which the caller drains).</summary>
    Friend Shared Function MaxAnisotropy() As Single
        Dim v As Single = 0
        GL.GetFloat(CType(MaxTextureMaxAnisotropyExt, GetPName), v)
        Return v
    End Function

    ''' <summary>GL_TEXTURE_MAX_ANISOTROPY_EXT of the texture bound to <paramref name="target"/>.</summary>
    Friend Shared Sub TexAnisotropy(target As TextureTarget, value As Single)
        GL.TexParameter(target, CType(TextureMaxAnisotropyExt, TextureParameterName), value)
    End Sub

    ''' <summary>GL_TEXTURE_MAX_ANISOTROPY_EXT of sampler object <paramref name="sampler"/>.</summary>
    Friend Shared Sub SamplerAnisotropy(sampler As Integer, value As Single)
        GL.SamplerParameter(sampler, CType(TextureMaxAnisotropyExt, SamplerParameterName), value)
    End Sub

    ' =================================================================================================================================
    ' Clip control (ARB_clip_control -> ClipGlsl + facing)
    ' =================================================================================================================================

    ''' <summary>The uniform buffer binding of the clip state, written ONCE (here) and interpolated into ClipGlsl; ClipBinding is
    ''' read from it. The clip block is the only uniform block the app's shaders may declare (GlslViolations): binding 0 is every
    ''' block's default, so it has one owner.</summary>
    Private Const ClipBindingGlsl As String = "0"
    Friend Shared ReadOnly ClipBinding As Integer = Integer.Parse(ClipBindingGlsl, Globalization.CultureInfo.InvariantCulture)

    ''' <summary>The clip conversion of every last vertex stage (VS, or DS when the program tessellates), inserted after #version:
    ''' gl_Position = gl43ClipToGl(the D3D clip position). y times the origin's sign (ARB_clip_control 13.6: y_d = f y_c / w_c, f = +1
    ''' LOWER_LEFT, -1 UPPER_LEFT); z = 2 z - w so that GL's NEGATIVE_ONE_TO_ONE window depth (z_d + 1) / 2 is the D3D z / w
    ''' (13.6.1: s = (f - n) / 2, b = (n + f) / 2 with the default depth range [0, 1]). The clip volume is the same: -w &lt;= 2 z - w
    ''' &lt;= w is 0 &lt;= z &lt;= w. 2 z is exact and the difference rounds once (fused or not): the same position from the same
    ''' D3D position in every program, which the invariant gl_Position of the prepass / colour / decal-base draws needs.</summary>
    Friend Const ClipGlsl As String = "// GL43 CLIP CONTROL (OpenGL 4.3 has no glClipControl): the D3D clip position the laws write, in GL clip space.
// y times the origin sign (+1 LOWER_LEFT, -1 UPPER_LEFT); z = 2 z - w: the NEGATIVE_ONE_TO_ONE window depth is then D3D z / w.
layout(std140, binding = " & ClipBindingGlsl & ") uniform Gl43ClipControl { vec4 gl43ClipOrigin; };
vec4 gl43ClipToGl(vec4 p) { return vec4(p.x, p.y * gl43ClipOrigin.x, 2.0 * p.z - p.w, p.w); }
"

    ''' <summary>The same function for the transform-feedback capture of an engine VS (Fo4DeferredPrograms.EnginePositionProgram,
    ''' rasterizer discard): the captured gl_Position is the D3D clip position the VS writes; the draw that reads it (an FO4 overlay's
    ''' effect VS, Fo4EffectSource.AppVertexFoot) converts it with ClipGlsl, as the host's own draw does.</summary>
    Friend Const ClipGlslCapture As String = "// GL43 CLIP CONTROL, TRANSFORM-FEEDBACK CAPTURE (rasterizer discard): gl_Position keeps the D3D clip position.
vec4 gl43ClipToGl(vec4 p) { return p; }
"

    Private NotInheritable Class ClipState
        Public Ubo As Integer
        Public Origin As ClipOrigin
        Public RequestedFront As FrontFaceDirection
        ''' <summary>The context's polygon offset per depth format (DepthFormat.Key): D16 / D24S8 / the default framebuffer measured at
        ''' InitContext, any other format on its first bias (DepthBias).</summary>
        Public ReadOnly OffsetUnits As New Dictionary(Of String, PolygonOffsetUnit)
        ''' <summary>The depth formats whose measurement failed, with the reason (the start and the frames go on; a bias drawn on such a
        ''' format is drawn without it and its reason returned by DepthBias).</summary>
        Public ReadOnly OffsetErrors As New Dictionary(Of String, String)
        ''' <summary>The wall time of those measurements in InitContext, ms (with the GL finish of the last read).</summary>
        Public OffsetMeasureMs As Double
        ''' <summary>The probe of the measurements (rev-E11: one per context, built on the first measurement; ReleaseContext deletes
        ''' it with the context's other objects - ola 7g round 2d).</summary>
        Public OffsetProbe As Fo4_Deferred_Shader_Class
        Public OffsetProbeVao As Integer
        ''' <summary>The default framebuffer's measurement attempts so far, and whether they are closed (rev-E11: attempts only up to
        ''' the context's first presented frame - DefaultFramebufferAttemptsClose -; after that a failure stays cached).</summary>
        Public DefaultAttempts As Integer
        Public DefaultAttemptsClosed As Boolean
    End Class

    Private Shared ReadOnly _clip As New ConcurrentDictionary(Of IntPtr, ClipState)()
#If DEBUG Then

    ''' <summary>The objects a context's clip state creates on it (ola 7g round 2f, r7g-01): the polygon-offset probe's program and its
    ''' VAO (MeasureOffsetOn) and the clip buffer (InitContext).</summary>
    Friend Enum GateClipKind
        Probe = 0
        ProbeVao = 1
        Ubo = 2
    End Enum

    ''' <summary>The Debug ledger of those objects by context (ola 7g round 2f, r7g-01): six counts per context, [created, deleted] for
    ''' each GateClipKind in its order, counted where each object is created (InitContext, MeasureOffsetOn) and deleted (ReleaseContext).
    ''' Its law: GateClipLaw.</summary>
    Friend Shared ReadOnly GateClipObjects As New Dictionary(Of IntPtr, Integer())

    Private Shared Sub GateClipCount(ctx As IntPtr, kind As GateClipKind, created As Boolean)
        SyncLock GateClipObjects
            Dim c As Integer() = Nothing
            If Not GateClipObjects.TryGetValue(ctx, c) Then c = New Integer(5) {} : GateClipObjects(ctx) = c
            c(CInt(kind) * 2 + If(created, 0, 1)) += 1
        End SyncLock
    End Sub

    ''' <summary>THE LAW of a context its control released while it was alive (ReleaseContext with contextAlive, the context current):
    ''' every object its clip state created on it was deleted on it - created = deleted for the probe's program, its VAO and the clip
    ''' buffer, each one. A context the ledger does not know breaks it too (InitContext creates the clip buffer of every context: a
    ''' context with no count is not one this module set up). Nothing when the law holds; otherwise what does not.</summary>
    Friend Shared Function GateClipLaw(ctx As IntPtr) As String
        Dim c As Integer() = Nothing
        SyncLock GateClipObjects
            If Not GateClipObjects.TryGetValue(ctx, c) Then Return $"context 0x{ctx.ToInt64():X}: not in the clip ledger"
            c = CType(c.Clone(), Integer())
        End SyncLock
        Dim malos As New List(Of String)
        For Each k In {GateClipKind.Probe, GateClipKind.ProbeVao, GateClipKind.Ubo}
            Dim creados = c(CInt(k) * 2), borrados = c(CInt(k) * 2 + 1)
            If creados <> borrados Then malos.Add($"{k} created {creados}, deleted {borrados}")
        Next
        If c(CInt(GateClipKind.Ubo) * 2) = 0 Then malos.Add("no clip buffer created (InitContext did not run on it)")
        Return If(malos.Count = 0, Nothing, $"context 0x{ctx.ToInt64():X}: " & String.Join("; ", malos))
    End Function
#End If

    <DllImport("opengl32.dll", ExactSpelling:=True)>
    Private Shared Function wglGetCurrentContext() As IntPtr
    End Function
    ''' <summary>The current GL context's handle (the key of every per-context state: this module's clip state, ProgramCompiler's
    ''' stage objects and driver identity).</summary>
    Friend Shared Function CurrentContext() As IntPtr
        Return wglGetCurrentContext()
    End Function
#If DEBUG Then
    ''' <summary>Ola 7g round 2d tester (WmMismaEscenaGate --ola7g, R2-7): the current GL context, for the Debug ledger of programs by context.</summary>
    Friend Shared Function GateCurrentContext() As IntPtr
        Return wglGetCurrentContext()
    End Function
#End If

    Private Shared Function Current() As ClipState
        Dim st As ClipState = Nothing
        If Not _clip.TryGetValue(wglGetCurrentContext(), st) Then
            Throw New InvalidOperationException("Gl43: the current GL context was not initialised (Gl43.InitContext)")
        End If
        Return st
    End Function

    ''' <summary>The current context's clip state, set up once right after the context is created (PreviewControl.OnLoad): the
    ''' context must be OpenGL 4.3 or newer (what this module calls), the clip uniform buffer at ClipBinding with LOWER_LEFT, front
    ''' face CCW. Returns the context's handle, which ReleaseContext takes.</summary>
    Friend Shared Function InitContext() As IntPtr
        Dim ctx = wglGetCurrentContext()
        If ctx = IntPtr.Zero Then Throw New InvalidOperationException("Gl43.InitContext: no current GL context")
        Dim major = GL.GetInteger(GetPName.MajorVersion), minor = GL.GetInteger(GetPName.MinorVersion)
        If major < 4 OrElse (major = 4 AndAlso minor < 3) Then
            Throw New NotSupportedException($"OpenGL 4.3 is required; this context is {major}.{minor}")
        End If
        Dim st As New ClipState With {.Origin = ClipOrigin.LowerLeft, .RequestedFront = FrontFaceDirection.Ccw}
        st.Ubo = GL.GenBuffer()
#If DEBUG Then
        GateClipCount(ctx, GateClipKind.Ubo, True)
#End If
        Dim prev = GL.GetInteger(GetPName.UniformBufferBinding)
        GL.BindBuffer(BufferTarget.UniformBuffer, st.Ubo)
        GL.BufferData(BufferTarget.UniformBuffer, 16, New Single() {1.0F, 0.0F, 0.0F, 0.0F}, BufferUsageHint.DynamicDraw)
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, ClipBinding, st.Ubo)
        GL.BindBuffer(BufferTarget.UniformBuffer, prev)
        _clip(ctx) = st
        ' The context's program compiler state (its driver identity, binary formats and stage objects) before the first program: the
        ' polygon-offset probe below is built through it.
        ProgramCompiler.InitContext(ctx)
        GL.FrontFace(FrontFaceDirection.Ccw)
        ' The context's polygon offset (DepthBias's law), measured at the start on the frame's two depth formats and the default
        ' framebuffer; any other format on its first bias. A measurement that fails does not stop anything (rev-E05, v5): it is kept with
        ' its reason; the default framebuffer, often not drawable yet at OnLoad, is measured again by RenderScene.
        Dim sw = Diagnostics.Stopwatch.StartNew()
        For Each f In OffsetFormats
            Try
                st.OffsetUnits(f.Key) = MeasurePolygonOffset(f)
            Catch ex As Exception
                st.OffsetErrors(f.Key) = ex.Message
            End Try
        Next
        MeasureDefaultFramebufferIfNeeded()
        st.OffsetMeasureMs = sw.Elapsed.TotalMilliseconds
        Return ctx
    End Function

    ''' <summary>Releases the clip state of context <paramref name="ctx"/>, which must be the current one (PreviewControl.Clean, the
    ''' control's teardown, where every other object of the control is deleted on it): the objects this state created on the
    ''' context - the clip buffer (InitContext), the polygon-offset probe's program and VAO (MeasureOffsetOn) - are deleted by it,
    ''' then the state is forgotten (ola 7g round 2d: the probe's program was left to the context's destruction, one program per
    ''' control out of the count of programs deleted = created; measured, this call finds its context current on every release).
    ''' A context that is not the current one is not touched (its names would be deleted on another context; the preview's contexts
    ''' share no objects, so its own destruction frees them, as before): its state is forgotten and the case logged - a teardown
    ''' must not throw (PreviewControl.Release). <paramref name="contextAlive"/> False (the control released after its context,
    ''' PreviewControl.ReleasedWithoutContext - ola 7g round 2e): its objects went with it and the handle's value may already name
    ''' another context, so nothing is deleted nor compared - the state is only forgotten.</summary>
    Friend Shared Sub ReleaseContext(ctx As IntPtr, contextAlive As Boolean)
        Dim st As ClipState = Nothing
#If DEBUG Then
        If Not contextAlive AndAlso Fo4GBufferConstants.GateMutantOn("g7-r2e-gl43-keeps") Then Return
#End If
        If Not _clip.TryRemove(ctx, st) Then Return
        ' ProgramCompiler's state of the context goes with it: its stage objects deleted on it when it is alive and current, only
        ' forgotten otherwise (the same rule as the clip state's objects below).
        ProgramCompiler.ReleaseContext(ctx, contextAlive AndAlso wglGetCurrentContext() = ctx)
        If Not contextAlive Then Return
        If wglGetCurrentContext() <> ctx Then
            Logger.LogLazy(Function() "[GL43] ReleaseContext: the released context is not the current one - its clip buffer and offset probe are left to its destruction")
            Return
        End If
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("g7-r2d-release-no-deletes") Then Return
#End If
        If st.OffsetProbe IsNot Nothing Then
            st.OffsetProbe.Dispose() : st.OffsetProbe = Nothing
#If DEBUG Then
            GateClipCount(ctx, GateClipKind.Probe, False)
#End If
        End If
#If DEBUG Then
        ' r7g-01 tester: ReleaseContext without the VAO's and the clip buffer's deletes (the probe's program is deleted).
        If Fo4GBufferConstants.GateMutantOn("g7-r7g01-no-vao-ubo") Then Return
#End If
        If st.OffsetProbeVao <> 0 Then
#If DEBUG Then
            ' r7g-01 tester: the ledger counts the delete but GL never gets it (a lying counter).
            If Not Fo4GBufferConstants.GateMutantOn("g7-r7g01-lying-counter") Then GL.DeleteVertexArray(st.OffsetProbeVao)
            st.OffsetProbeVao = 0
#Else
            GL.DeleteVertexArray(st.OffsetProbeVao) : st.OffsetProbeVao = 0
#End If
#If DEBUG Then
            GateClipCount(ctx, GateClipKind.ProbeVao, False)
#End If
        End If
        If st.Ubo <> 0 Then
#If DEBUG Then
            If Not Fo4GBufferConstants.GateMutantOn("g7-r7g01-lying-counter") Then GL.DeleteBuffer(st.Ubo)
            st.Ubo = 0
#Else
            GL.DeleteBuffer(st.Ubo) : st.Ubo = 0
#End If
#If DEBUG Then
            GateClipCount(ctx, GateClipKind.Ubo, False)
#End If
        End If
    End Sub

    ''' <summary>glClipControl(origin, ZERO_TO_ONE): the origin's sign into the clip buffer and the facing it implies (ARB_clip_control
    ''' 14.6.1: UPPER_LEFT negates the polygon area, so the front face stays the requested one).</summary>
    Friend Shared Sub ClipControl(origin As ClipOrigin)
        If origin <> ClipOrigin.LowerLeft AndAlso origin <> ClipOrigin.UpperLeft Then Throw New ArgumentException("Gl43.ClipControl: " & origin.ToString(), NameOf(origin))
        Dim st = Current()
        st.Origin = origin
        Dim prev = GL.GetInteger(GetPName.UniformBufferBinding)
        GL.BindBuffer(BufferTarget.UniformBuffer, st.Ubo)
        GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, 16, New Single() {If(origin = ClipOrigin.UpperLeft, -1.0F, 1.0F), 0.0F, 0.0F, 0.0F})
        GL.BindBuffer(BufferTarget.UniformBuffer, prev)
        ApplyFront(st)
    End Sub

    ''' <summary>glUseProgram with the clip contract checked: a program (every vertex stage reads Gl43ClipControl) is bound only
    ''' while the current context's clip buffer is the one at ClipBinding; otherwise InvalidOperationException (no draw reads an
    ''' unbound uniform block, whose contents GL leaves undefined). UseProgram(0) is not checked.</summary>
    Friend Shared Sub UseProgram(program As Integer)
#If DEBUG Then
        ' Ola 7f tester (ShadowGate --ola7f e1 / e4): every program a draw binds, in order, while a gate holds the list.
        If program <> 0 AndAlso GateUseLog IsNot Nothing Then GateUseLog.Add(program)
#End If
        If program <> 0 Then
            Dim st = Current()
            Dim bound As Integer
            GL.GetInteger(GetIndexedPName.UniformBufferBinding, ClipBinding, bound)
            If bound <> st.Ubo Then Throw New InvalidOperationException($"Gl43.UseProgram: uniform buffer binding {ClipBinding} holds {bound}, not the clip state's {st.Ubo}")
        End If
        GL.UseProgram(program)
    End Sub

#If DEBUG Then
    ''' <summary>DEBUG ONLY (ola 7f tester, ShadowGate --ola7f): the GL program ids UseProgram bound, in order, while not Nothing.</summary>
    Friend Shared GateUseLog As List(Of Integer) = Nothing
#End If

#If DEBUG Then
    ''' <summary>DEBUG ONLY (ShadowGate --fo4-deferred-scene, law gl43-unbound-clip): <paramref name="attached"/> False detaches the
    ''' clip buffer from ClipBinding, True puts it back.</summary>
    Friend Shared Sub GateClipBufferAttached(attached As Boolean)
        Dim st = Current()
        Dim prev = GL.GetInteger(GetPName.UniformBufferBinding)
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, ClipBinding, If(attached, st.Ubo, 0))
        GL.BindBuffer(BufferTarget.UniformBuffer, prev)
    End Sub

    ''' <summary>DEBUG ONLY (the gates' check): the current context's clip uniform buffer (the one at ClipBinding): what every vertex
    ''' stage reads.</summary>
    Friend Shared ReadOnly Property ClipBuffer As Integer
        Get
            Return Current().Ubo
        End Get
    End Property
#End If

    ''' <summary>The current clip origin (ClipControl's last value; LOWER_LEFT after InitContext).</summary>
    Friend Shared ReadOnly Property Origin As ClipOrigin
        Get
            Return Current().Origin
        End Get
    End Property

    ''' <summary>glFrontFace under the emulated origin: the requested direction, inverted while the origin is UPPER_LEFT.</summary>
    Friend Shared Sub FrontFace(direction As FrontFaceDirection)
        Dim st = Current()
        st.RequestedFront = direction
        ApplyFront(st)
    End Sub

    Private Shared Sub ApplyFront(st As ClipState)
        Dim d = st.RequestedFront
        If st.Origin = ClipOrigin.UpperLeft Then d = If(d = FrontFaceDirection.Ccw, FrontFaceDirection.Cw, FrontFaceDirection.Ccw)
        GL.FrontFace(d)
    End Sub

    ' =================================================================================================================================
    ' D3D11 DEPTH BIAS (ola 4 v3)
    ' =================================================================================================================================

    ''' <summary>THE DEPTH BIAS OF A D3D RASTERIZER STATE IN GL 4.3 (D3D11.3 15.10): on a UNORM depth buffer Bias = DepthBias x r +
    ''' SlopeScaledDepthBias x MaxDepthSlope, r = 1 / (2^n - 1) (the format's least positive value: ONE code), MaxDepthSlope =
    ''' max(|dz/dx|, |dz/dy|), then DepthBiasClamp, then the biased z clamped to the viewport's depth range. GL 4.3 14.6.5 adds
    ''' o = m x factor + r_GL x units with r_GL left to the implementation; after the offset a fixed-point depth is clamped to [0, 1].
    ''' Measured on NVIDIA RTX 2080 Ti, driver 610.88 (ShadowGate --gl-depth-bias, 14-oct-2026): one GL unit = 2 codes on 16- and on
    ''' 24-bit UNORM - the D3D units cannot be passed through (the v2 law drew the shadow map's DepthBias 12 as 24 codes; gate G-4a).
    ''' The law of the constant term: units = DepthBias / (codes one GL unit adds, MEASURED per format on this context:
    ''' PolygonOffsetUnitOf).
    ''' <para>THE SLOPE TERM (USER DECISION 14-oct-2026): factor = SlopeScaledDepthBias as it is, and the GPU computes m - GL 4.3 eq.
    ''' 14.10 sqrt(zx^2 + zy^2) or eq. 14.11 max(|zx|, |zy|), D3D's, as the implementation chooses. No detection, no notice.</para>
    ''' <para>DepthBiasClamp is not applied (GL 4.3 has no offset clamp): every rasterizer state of the two games has -100 (FO4
    ''' 0x1424875E0, SSE 0x14102B6DF; ParityGate's bias table checks FO4's), and on a UNORM target z + bias leaves [0, 1] before
    ''' |bias| reaches 1, where D3D and GL both clamp the biased z: a clamp of magnitude 1 or more never changes a stored code.</para>
    ''' <para>DECISION (ola 4 v3, with numbers): the bias goes through glPolygonOffset with the measured unit, not written by the fragment
    ''' stage (gl_FragDepth = z + DepthBias r + Slope max(|dFdx z|, |dFdy z|)). (1) Law: 128 of the 470 b09 PS declare
    ''' forceEarlyDepthStencil (rec2762, 2115 corpus shapes, among them) and GLSL's early_fragment_tests ignores gl_FragDepth (GLSL 4.30
    ''' 4.4.1.3): D3D tests those fragments with the biased depth before the PS, so a decal of such a record would lose its bias.
    ''' (2) Cost (ShadowGate --gl-depth-bias, 4096 x 4096 D16, 8 full layers): 0.078 / 0.240 ms front-to-back / back-to-front with the
    ''' offset, 1.93 / 1.93 ms writing gl_FragDepth, 0.289 / 1.93 ms with layout(depth_greater).</para>
    ''' <para>The unit is the one of the depth FORMAT the current draw framebuffer has (DrawDepthFormat: the default framebuffer, or
    ''' an attachment's format), so every draw takes the unit of the buffer it writes. A format not measured yet is measured ON DEMAND
    ''' the first time a bias is drawn on it - in this context, every state it touches put back - and cached (v5, decided by the
    ''' coordinator 15-oct-2026: a new depth format must not break the app; the v4 aborted every later frame). A float depth has the
    ''' same law in both APIs (D3D11.3 15.10 and GL 4.3 14.6.5: r = 2^(e - 23), e the exponent of the primitive's largest z) and is
    ''' measured the same way, in units of 2^-25 at the probe's z 0.25. Only a measurement that FAILS leaves this draw without its
    ''' bias: the offset off and the reason returned (the caller lists the shape in the frame's notice); the frame and the process go
    ''' on, and the failure is cached (no new attempt per draw). The default framebuffer holds the frame and is not measured here:
    ''' InitContext and PreviewControl.RenderScene (before it clears the window) measure it. Returns Nothing when the bias is applied
    ''' or there is nothing to bias (no depth buffer, or both terms 0).</para></summary>
    Friend Shared Function DepthBias(constantBias As Single, slopeScaledDepthBias As Single, depthBiasClamp As Single) As String
        If constantBias = 0.0F AndAlso slopeScaledDepthBias = 0.0F Then
            GL.Disable(EnableCap.PolygonOffsetFill)
            Return Nothing
        End If
        Dim st = Current()
        Dim fmt = DrawDepthFormat()
        If Not fmt.HasValue Then
            GL.Disable(EnableCap.PolygonOffsetFill)                  ' no depth buffer: nothing to bias
            Return Nothing
        End If
        Dim key = fmt.Value.Key
        Dim u As PolygonOffsetUnit
        If Not st.OffsetUnits.TryGetValue(key, u) AndAlso Not st.OffsetErrors.ContainsKey(key) AndAlso Not fmt.Value.IsDefault Then
            Try
                u = MeasurePolygonOffset(fmt.Value)
                st.OffsetUnits(key) = u
            Catch ex As Exception
                st.OffsetErrors(key) = ex.Message
            End Try
        End If
        If Not st.OffsetUnits.TryGetValue(key, u) Then
            GL.Disable(EnableCap.PolygonOffsetFill)
            Dim e As String = Nothing
            Return $"drawn without its depth bias: the polygon offset unit of the {key} depth " &
                   If(st.OffsetErrors.TryGetValue(key, e), $"could not be measured ({e})", "is not measured yet")
        End If
        GL.Enable(EnableCap.PolygonOffsetFill)
        GL.PolygonOffset(slopeScaledDepthBias, CSng(constantBias / u.CodesPerUnit))
        Return Nothing
    End Function

    ''' <summary>The default framebuffer's unit, measured where the window is drawn when InitContext could not: the probe at OnLoad finds
    ''' a window not yet drawable (GL 4.3 14.9.1, pixel ownership; measured 14 / 15-oct-2026: 0 codes for 4096 units). PreviewControl.
    ''' RenderScene calls it on its direct frame right before clearing the window, so the probe's depth is cleared by the frame itself;
    ''' a gate calls it once the window is drawable. Nothing after a success; until then each call measures again (a failure is kept
    ''' as the buffer's reason). Returns the unit, or Nothing (no depth buffer, or the measurement failed).</summary>
    Friend Shared Function MeasureDefaultFramebufferIfNeeded() As PolygonOffsetUnit?
        Dim st = Current()
        Dim u As PolygonOffsetUnit
        If st.OffsetUnits.TryGetValue(DepthKeyDefault, u) Then Return u
        If st.DefaultAttemptsClosed Then Return Nothing                ' rev-E11: the failure stays cached after the first presented frame
        st.DefaultAttempts += 1
        Try
            Dim m = MeasureDefaultFramebufferOffset()
            If m.HasValue Then st.OffsetUnits(DepthKeyDefault) = m.Value : st.OffsetErrors.Remove(DepthKeyDefault)
            Return m
        Catch ex As Exception
            st.OffsetErrors(DepthKeyDefault) = ex.Message
            Return Nothing
        End Try
    End Function

    ''' <summary>rev-E11: the context presented its first frame (PreviewControl.RenderScene, after the frame): no more attempts at the
    ''' default framebuffer's unit - the start and the frames up to this one had them, and the window was drawable for this one -; a
    ''' failure stays cached and a bias on the default framebuffer returns its reason.</summary>
    Friend Shared Sub DefaultFramebufferAttemptsClose()
        Current().DefaultAttemptsClosed = True
    End Sub

#If DEBUG Then
    ''' <summary>DEBUG ONLY (the gates): how many times this context measured the default framebuffer (rev-E11, the bound of the
    ''' retries).</summary>
    Friend Shared ReadOnly Property DefaultFramebufferAttempts As Integer
        Get
            Return Current().DefaultAttempts
        End Get
    End Property
#End If

    ''' <summary>The keys of the depth formats (DepthFormat.Key): the two the start measures - DepthComponent16 and Depth24Stencil8, the
    ''' textures and renderbuffers of the frame - and the window's default framebuffer. Any other format is measured on its first bias.
    ''' </summary>
    Friend Const DepthKeyD16 As String = "D16"
    Friend Const DepthKeyD24S8 As String = "D24S8"
    Friend Const DepthKeyDefault As String = "default framebuffer"

    ''' <summary>A depth format as a draw framebuffer's attachment reports it (GL 4.3 9.2.3, table 9.2: FRAMEBUFFER_ATTACHMENT_DEPTH_SIZE,
    ''' _STENCIL_SIZE - the sizes of the attached image's format - and _COMPONENT_TYPE), or the default framebuffer.</summary>
    Friend Structure DepthFormat
        Public Bits As Integer, StencilBits As Integer, IsFloat As Boolean, IsDefault As Boolean
        ''' <summary>"D16", "D24", "D24S8", "D32", "D32F", "D32FS8", ... or DepthKeyDefault.</summary>
        Public ReadOnly Property Key As String
            Get
                If IsDefault Then Return DepthKeyDefault
                Return $"D{Bits}{If(IsFloat, "F", "")}{If(StencilBits > 0, $"S{StencilBits}", "")}"
            End Get
        End Property
        ''' <summary>The sized format of a texture of this format (the on-demand probe target); Nothing when GL 4.3 has none.</summary>
        Friend Function Sized() As SizedInternalFormat?
            Select Case Key
                Case "D16" : Return SizedInternalFormat.DepthComponent16
                Case "D24" : Return SizedInternalFormat.DepthComponent24
                Case "D24S8" : Return SizedInternalFormat.Depth24Stencil8
                Case "D32" : Return SizedInternalFormat.DepthComponent32
                Case "D32F" : Return SizedInternalFormat.DepthComponent32f
                Case "D32FS8" : Return SizedInternalFormat.Depth32fStencil8
                Case Else : Return Nothing
            End Select
        End Function
    End Structure

    ''' <summary>What one unit of glPolygonOffset adds on a depth format of the current context (MeasureOffsetOn).</summary>
    Friend Structure PolygonOffsetUnit
        ''' <summary>Codes of the format one GL unit adds on a plane of slope 0 (a float depth: units of 2^-25 at z 0.25, D3D's r there).
        ''' </summary>
        Public CodesPerUnit As Double
        ''' <summary>The measurement, for the gate: the units drawn and the codes they added (all pixels; Min = Max on a flat plane).</summary>
        Public UnitCodesMin, UnitCodesMax, UnitsDrawn As Integer
    End Structure

    ''' <summary>The wall time (ms) InitContext took to measure the current context's polygon offset units (MeasurePolygonOffset on
    ''' every format; each ends reading the target back, so the GPU work is inside).</summary>
    Friend Shared ReadOnly Property PolygonOffsetMeasureMs As Double
        Get
            Return Current().OffsetMeasureMs
        End Get
    End Property

    ''' <summary>The measured unit of depth format <paramref name="key"/> (DepthFormat.Key) on the current context, Nothing when it has
    ''' none (not measured yet, the measurement failed - PolygonOffsetErrorOf -, or the default framebuffer has no depth).</summary>
    Friend Shared Function PolygonOffsetUnitOf(key As String) As PolygonOffsetUnit?
        Dim u As PolygonOffsetUnit
        If Current().OffsetUnits.TryGetValue(key, u) Then Return u
        Return Nothing
    End Function

    ''' <summary>Why the measurement of depth format <paramref name="key"/> failed, Nothing when it did not.</summary>
    Friend Shared Function PolygonOffsetErrorOf(key As String) As String
        Dim e As String = Nothing
        Return If(Current().OffsetErrors.TryGetValue(key, e), e, Nothing)
    End Function

#If DEBUG Then
    ''' <summary>DEBUG ONLY (ShadowGate --gl-depth-bias): the on-demand measurement of the next NEW format fails (a measurement that
    ''' cannot be made), so the gate sees that draw without its bias and the frames after it drawn.</summary>
    Friend Shared GateFailNextMeasurement As Boolean = False

    ''' <summary>DEBUG ONLY (ShadowGate --gl-depth-bias): forgets depth format <paramref name="key"/>'s cached unit or failure, so the
    ''' next bias on it measures it again (the gate's mutants need a measurement on a format the run already measured).</summary>
    Friend Shared Sub GateForgetUnit(key As String)
        Dim st = Current()
        st.OffsetUnits.Remove(key) : st.OffsetErrors.Remove(key)
    End Sub
#End If

    ''' <summary>The depth format of the current draw framebuffer: the default framebuffer, or its DEPTH_ATTACHMENT's; Nothing when
    ''' there is no depth buffer.</summary>
    Private Shared Function DrawDepthFormat() As DepthFormat?
        Dim isDefault = GL.GetInteger(GetPName.DrawFramebufferBinding) = 0
        Dim att = If(isDefault, FramebufferAttachment.Depth, FramebufferAttachment.DepthAttachment)
        Dim objType, bits, stencil, kind As Integer
        GL.GetFramebufferAttachmentParameter(FramebufferTarget.DrawFramebuffer, att, FramebufferParameterName.FramebufferAttachmentObjectType, objType)
        If objType = CInt(All.None) Then Return Nothing
        GL.GetFramebufferAttachmentParameter(FramebufferTarget.DrawFramebuffer, att, FramebufferParameterName.FramebufferAttachmentDepthSize, bits)
        If bits = 0 Then Return Nothing
        If isDefault Then Return New DepthFormat With {.Bits = bits, .IsDefault = True}
        GL.GetFramebufferAttachmentParameter(FramebufferTarget.DrawFramebuffer, att, FramebufferParameterName.FramebufferAttachmentStencilSize, stencil)
        GL.GetFramebufferAttachmentParameter(FramebufferTarget.DrawFramebuffer, att, FramebufferParameterName.FramebufferAttachmentComponentType, kind)
        Return New DepthFormat With {.Bits = bits, .StencilBits = stencil, .IsFloat = kind = CInt(All.Float)}
    End Function

    ''' <summary>The formats InitContext measures.</summary>
    Private Shared ReadOnly OffsetFormats As DepthFormat() = {New DepthFormat With {.Bits = 16}, New DepthFormat With {.Bits = 24, .StencilBits = 8}}

    ''' <summary>The probe's vertex stage: the full-viewport triangle (gl_VertexID 0..2: (-1, -1), (3, -1), (-1, 3)) on the plane of
    ''' window depth uPlane.x + uPlane.y x_ndc + uPlane.z y_ndc (a D3D clip position with w = 1, through ClipGlsl).</summary>
    Friend Const OffsetProbeVertex As String = "#version 430
" & ClipGlsl & "uniform vec4 uPlane;
void main()
{
    vec2 p = vec2((gl_VertexID == 1) ? 3.0 : -1.0, (gl_VertexID == 2) ? 3.0 : -1.0);
    gl_Position = gl43ClipToGl(vec4(p, uPlane.x + uPlane.y * p.x + uPlane.z * p.y, 1.0));
}
"

    ''' <summary>The probe's fragment stage: none of its own (the depth is the rasterizer's).</summary>
    Friend Const OffsetProbeFragment As String = "#version 430
void main()
{
}
"

    ''' <summary>The codes of a depth read back exactly: UNORM 16 UNSIGNED_SHORT; UNORM 24 + 8 UNSIGNED_INT_24_8 (the depth in the
    ''' high 24 bits); other UNORM UNSIGNED_INT, c = round(v (2^n - 1) / (2^32 - 1)) (GL 4.3 8.4.4.2 / 18.2.6: the depth goes through
    ''' [0, 1]; at the probe's z near 0.25 the round trip is exact for n &lt;= 24 and n = 32); float FLOAT, c = z x 2^25 (z in [0.25, 0.5)
    ''' is a multiple of 2^-25; the cleared 1.0 is 2^25). <paramref name="read"/> reads (format, type, buffer) into the buffer.</summary>
    Private Shared Sub ReadCodes(fmt As DepthFormat, n As Integer, codes As Long(), read As Action(Of PixelFormat, PixelType, Object))
        If fmt.IsFloat Then
            Dim f(n - 1) As Single
            read(PixelFormat.DepthComponent, PixelType.Float, f)
            For i = 0 To n - 1 : codes(i) = CLng(Math.Round(CDbl(f(i)) * 33554432.0)) : Next
        ElseIf fmt.Bits = 16 Then
            Dim d(n - 1) As UShort
            read(PixelFormat.DepthComponent, PixelType.UnsignedShort, d)
            For i = 0 To n - 1 : codes(i) = d(i) : Next
        ElseIf fmt.Bits = 24 AndAlso fmt.StencilBits = 8 Then
            Dim d(n - 1) As UInteger
            read(PixelFormat.DepthStencil, PixelType.UnsignedInt248, d)
            For i = 0 To n - 1 : codes(i) = d(i) >> 8 : Next
        Else
            Dim d(n - 1) As UInteger
            read(PixelFormat.DepthComponent, PixelType.UnsignedInt, d)
            Dim m = Math.Pow(2.0, fmt.Bits) - 1.0
            For i = 0 To n - 1 : codes(i) = CLng(Math.Round(d(i) * m / 4294967295.0)) : Next
        End If
    End Sub

    ''' <summary>The code a cleared depth (1.0) reads as: 2^n - 1, or 2^25 for a float depth (ReadCodes).</summary>
    Private Shared Function ClearedCode(fmt As DepthFormat) As Long
        Return If(fmt.IsFloat, 33554432L, (1L << fmt.Bits) - 1L)
    End Function

    ''' <summary>Measures glPolygonOffset on depth format <paramref name="fmt"/> (InitContext for D16 / D24S8; DepthBias on demand for
    ''' any other): a 64 x 64 texture of the format, depth test ALWAYS with writes, the probe's plane drawn without and with an offset,
    ''' the codes read back exactly (ReadCodes). The unit: a flat plane at z 0.25 with 4096 units (CodesPerUnit = the codes added /
    ''' 4096: one code of error is 1 / 4096 of a unit). The texture and framebuffer are its own, deleted after; the bindings and state
    ''' it touches are put back (it can run inside a frame).</summary>
    Private Shared Function MeasurePolygonOffset(fmt As DepthFormat) As PolygonOffsetUnit
        Const N As Integer = 64
#If DEBUG Then
        If GateFailNextMeasurement Then GateFailNextMeasurement = False : Throw New InvalidOperationException($"{fmt.Key}: gate mutant, the measurement fails")
#End If
        Dim sized = fmt.Sized()
        If Not sized.HasValue Then Throw New NotSupportedException($"{fmt.Key}: no sized depth format to measure it on")
        Dim tex = CreateTexture(TextureTarget.Texture2D)
        Dim fbo = 0
        Dim pack = GL.GetInteger(GetPName.PackAlignment)
        Try
            TexStorage2D(tex, TextureTarget2d.Texture2D, 1, sized.Value, N, N)
            fbo = CreateFramebuffer()
            FramebufferTexture(fbo, If(fmt.StencilBits > 0, FramebufferAttachment.DepthStencilAttachment, FramebufferAttachment.DepthAttachment), tex, 0)
            DrawBuffer(fbo, DrawBufferMode.None)
            If CheckFramebufferStatus(fbo, FramebufferTarget.Framebuffer) <> FramebufferStatus.FramebufferComplete Then
                Throw New InvalidOperationException($"a {fmt.Key} target is incomplete")
            End If
            Return MeasureOffsetOn(fbo, N, N, fmt,
                                   Sub()
                                       If fmt.StencilBits = 0 Then
                                           ClearFramebuffer(fbo, ClearBuffer.Depth, 0, New Single() {1.0F})
                                       Else
                                           ClearFramebuffer(fbo, ClearBufferCombined.DepthStencil, 0, 1.0F, 0)
                                       End If
                                   End Sub,
                                   Function(codes As Long())
                                       GL.PixelStore(PixelStoreParameter.PackAlignment, 4)
                                       ReadCodes(fmt, N * N, codes,
                                                 Sub(pf As PixelFormat, pt As PixelType, buf As Object)
                                                     Dim bytes = If(TypeOf buf Is UShort(), 2, 4) * N * N
                                                     Select Case True
                                                         Case TypeOf buf Is UShort() : GetTexImage(tex, TextureTarget.Texture2D, 0, pf, pt, bytes, DirectCast(buf, UShort()))
                                                         Case TypeOf buf Is UInteger() : GetTexImage(tex, TextureTarget.Texture2D, 0, pf, pt, bytes, DirectCast(buf, UInteger()))
                                                         Case Else : GetTexImage(tex, TextureTarget.Texture2D, 0, pf, pt, bytes, DirectCast(buf, Single()))
                                                     End Select
                                                 End Sub)
                                       Return True
                                   End Function)
        Finally
            GL.PixelStore(PixelStoreParameter.PackAlignment, pack)
            If fbo <> 0 Then GL.DeleteFramebuffer(fbo)
            GL.DeleteTexture(tex)
        End Try
    End Function

    ''' <summary>Measures glPolygonOffset on the window's default framebuffer (InitContext; rev-E05: its depth is the window's pixel
    ''' format, not one of the measured textures, so it is measured itself): the probe drawn into the lower-left min(64, width) x
    ''' min(64, height) pixels of the BACK buffer with colour writes off, the depth read back exactly (glReadPixels: D16
    ''' UNSIGNED_SHORT, D24S8 UNSIGNED_INT_24_8), the depth cleared after (every frame clears it before drawing). Nothing when the
    ''' default framebuffer has no depth buffer; throws (kept as the buffer's error) for any other depth format or an empty window.
    ''' </summary>
    Private Shared Function MeasureDefaultFramebufferOffset() As PolygonOffsetUnit?
        Dim prevDraw = GL.GetInteger(GetPName.DrawFramebufferBinding), prevRead = GL.GetInteger(GetPName.ReadFramebufferBinding)
        Try
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
            Dim objType, bits, stencil, kind As Integer
            GL.GetFramebufferAttachmentParameter(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.Depth, FramebufferParameterName.FramebufferAttachmentObjectType, objType)
            If objType = CInt(All.None) Then Return Nothing
            GL.GetFramebufferAttachmentParameter(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.Depth, FramebufferParameterName.FramebufferAttachmentDepthSize, bits)
            If bits = 0 Then Return Nothing
            GL.GetFramebufferAttachmentParameter(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.Stencil, FramebufferParameterName.FramebufferAttachmentStencilSize, stencil)
            GL.GetFramebufferAttachmentParameter(FramebufferTarget.DrawFramebuffer, FramebufferAttachment.Depth, FramebufferParameterName.FramebufferAttachmentComponentType, kind)
            Dim fmt As New DepthFormat With {.Bits = bits, .StencilBits = stencil, .IsFloat = kind = CInt(All.Float)}
            Dim vp(3) As Integer : GL.GetInteger(GetPName.Viewport, vp)
            Dim w = Math.Min(64, vp(2)), h = Math.Min(64, vp(3))
            If w <= 0 OrElse h <= 0 Then Throw New InvalidOperationException("the window has no pixels at the start")
            Dim clearDepth = Sub()
                                 If stencil = 0 Then
                                     ClearFramebuffer(0, ClearBuffer.Depth, 0, New Single() {1.0F})
                                 Else
                                     ClearFramebuffer(0, ClearBufferCombined.DepthStencil, 0, 1.0F, 0)
                                 End If
                             End Sub
            Dim cm(3) As Boolean : GL.GetBoolean(GetPName.ColorWritemask, cm)
            GL.ColorMask(False, False, False, False)
            Try
                Dim pack = GL.GetInteger(GetPName.PackAlignment)
                Try
                    Return MeasureOffsetOn(0, w, h, fmt, clearDepth,
                                           Function(codes As Long())
                                               GL.PixelStore(PixelStoreParameter.PackAlignment, 4)
                                               ReadCodes(fmt, w * h, codes,
                                                         Sub(pf As PixelFormat, pt As PixelType, buf As Object)
                                                             Select Case True
                                                                 Case TypeOf buf Is UShort() : GL.ReadPixels(0, 0, w, h, pf, pt, DirectCast(buf, UShort()))
                                                                 Case TypeOf buf Is UInteger() : GL.ReadPixels(0, 0, w, h, pf, pt, DirectCast(buf, UInteger()))
                                                                 Case Else : GL.ReadPixels(0, 0, w, h, pf, pt, DirectCast(buf, Single()))
                                                             End Select
                                                         End Sub)
                                               Return True
                                           End Function)
                Finally
                    GL.PixelStore(PixelStoreParameter.PackAlignment, pack)
                End Try
            Finally
                GL.ColorMask(cm(0), cm(1), cm(2), cm(3))
                clearDepth()
            End Try
        Finally
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, prevDraw)
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, prevRead)
        End Try
    End Function

    ''' <summary>The measurement itself, on framebuffer <paramref name="fbo"/> (0 = the default) over its lower-left w x h pixels:
    ''' depth test ALWAYS with writes, the probe's flat plane at z 0.25 drawn without and with 4096 units, every pixel's codes read
    ''' by <paramref name="read"/>. On a flat plane GL 4.3 14.6.5 adds the same o = r x units to every fragment of the same depth, so
    ''' every pixel must gain the same codes: a pixel that does not (a pixel the window does not own, 14.9.1, or a driver fault) is
    ''' not a measurement and throws. CodesPerUnit = codes added / 4096 (one code of error is 1 / 4096 of a unit).</summary>
    Private Shared Function MeasureOffsetOn(fbo As Integer, w As Integer, h As Integer, fmt As DepthFormat, clearDepth As Action,
                                            read As Func(Of Long(), Boolean)) As PolygonOffsetUnit
        Dim bits = fmt.Key
        Dim st = Current()
        Dim prevFbo = GL.GetInteger(GetPName.DrawFramebufferBinding), prevRead = GL.GetInteger(GetPName.ReadFramebufferBinding)
        Dim vp(3) As Integer : GL.GetInteger(GetPName.Viewport, vp)
        ' the state it changes, put back as it was (it runs inside a frame: DepthBias on demand, MeasureDefaultFramebufferIfNeeded)
        Dim depthTest = GL.IsEnabled(EnableCap.DepthTest), cull = GL.IsEnabled(EnableCap.CullFace), stencil = GL.IsEnabled(EnableCap.StencilTest)
        Dim scissor = GL.IsEnabled(EnableCap.ScissorTest), discardRaster = GL.IsEnabled(EnableCap.RasterizerDiscard)
        Dim offsetFill = GL.IsEnabled(EnableCap.PolygonOffsetFill)
        Dim depthFunc = GL.GetInteger(GetPName.DepthFunc), depthMask = GL.GetBoolean(GetPName.DepthWritemask)
        Dim prevProg = GL.GetInteger(GetPName.CurrentProgram), prevVao = GL.GetInteger(GetPName.VertexArrayBinding)
        Dim prevFactor = GL.GetFloat(GetPName.PolygonOffsetFactor), prevUnits = GL.GetFloat(GetPName.PolygonOffsetUnits)
        ' rev-E11: the polygon mode (core: one mode for both faces, GL 4.3 14.6.4) and the user clip planes in use
        Dim polyMode(1) As Integer : GL.GetInteger(GetPName.PolygonMode, polyMode)
        Dim clipOn(GL.GetInteger(GetPName.MaxClipDistances) - 1) As Boolean
        For i = 0 To clipOn.Length - 1 : clipOn(i) = GL.IsEnabled(CType(CInt(EnableCap.ClipDistance0) + i, EnableCap)) : Next
        Try
            If st.OffsetProbe Is Nothing Then
                st.OffsetProbe = New Fo4_Deferred_Shader_Class(OffsetProbeVertex, OffsetProbeFragment)
#If DEBUG Then
                GateClipCount(wglGetCurrentContext(), GateClipKind.Probe, True)
#End If
                st.OffsetProbeVao = GL.GenVertexArray()
#If DEBUG Then
                GateClipCount(wglGetCurrentContext(), GateClipKind.ProbeVao, True)
#End If
            End If
            Dim prog = st.OffsetProbe
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)
            GL.Viewport(0, 0, w, h)
            GL.Enable(EnableCap.DepthTest) : GL.DepthFunc(DepthFunction.Always) : GL.DepthMask(True)
            GL.Disable(EnableCap.CullFace) : GL.Disable(EnableCap.StencilTest)
            GL.Disable(EnableCap.ScissorTest) : GL.Disable(EnableCap.RasterizerDiscard)
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
            For i = 0 To clipOn.Length - 1 : GL.Disable(CType(CInt(EnableCap.ClipDistance0) + i, EnableCap)) : Next
            prog.Use()
            GL.BindVertexArray(st.OffsetProbeVao)
            Dim draw = Function(units As Single) As Long()
                           clearDepth()
                           prog.SetVector4("uPlane", New OpenTK.Mathematics.Vector4(0.25F, 0.0F, 0.0F, 0.0F))
                           If units = 0.0F Then
                               GL.Disable(EnableCap.PolygonOffsetFill)
                           Else
                               GL.Enable(EnableCap.PolygonOffsetFill) : GL.PolygonOffset(0.0F, units)
                           End If
                           GL.DrawArrays(PrimitiveType.Triangles, 0, 3)
                           GL.Disable(EnableCap.PolygonOffsetFill)
                           Dim codes(w * h - 1) As Long
                           read(codes)
                           Return codes
                       End Function
            Const U As Integer = 4096
            Dim a0 = draw(0.0F), a1 = draw(U)
            Dim cleared = ClearedCode(fmt)
            If a0.Any(Function(c) c = cleared) Then Throw New InvalidOperationException($"{bits}: the probe's plane was not written ({a0.Count(Function(c) c = cleared)} of {w}x{h} pixels keep the cleared depth)")
            Dim dMin = Long.MaxValue, dMax = Long.MinValue
            For i = 0 To a0.Length - 1
                Dim dd = a1(i) - a0(i)
                dMin = Math.Min(dMin, dd) : dMax = Math.Max(dMax, dd)
            Next
            If dMin <> dMax Then Throw New InvalidOperationException($"{bits}: a flat plane gained {dMin}..{dMax} codes over {w}x{h} pixels (one value expected)")
            If dMin <= 0 Then Throw New InvalidOperationException($"{bits}: {U} units added {dMin} codes (GL 4.3 14.6.5: a positive offset expected)")
            Dim r As PolygonOffsetUnit
            r.UnitsDrawn = U : r.UnitCodesMin = CInt(dMin) : r.UnitCodesMax = CInt(dMax)
            r.CodesPerUnit = CDbl(dMin) / U
            Return r
        Finally
            GL.BindVertexArray(prevVao)
#If DEBUG Then
            ' ShadowGate --gl-depth-bias's mutant (rev-E12): the program is not put back
            If Not Fo4GBufferConstants.GateMutantOn("bias-probe-no-program-restore") Then GL.UseProgram(prevProg)
#Else
            GL.UseProgram(prevProg)
#End If
            GL.PolygonMode(TriangleFace.FrontAndBack, CType(polyMode(0), PolygonMode))
            For i = 0 To clipOn.Length - 1
                If clipOn(i) Then GL.Enable(CType(CInt(EnableCap.ClipDistance0) + i, EnableCap)) Else GL.Disable(CType(CInt(EnableCap.ClipDistance0) + i, EnableCap))
            Next
            GL.DepthFunc(CType(depthFunc, DepthFunction)) : GL.DepthMask(depthMask)
            If depthTest Then GL.Enable(EnableCap.DepthTest) Else GL.Disable(EnableCap.DepthTest)
            If cull Then GL.Enable(EnableCap.CullFace) Else GL.Disable(EnableCap.CullFace)
            If stencil Then GL.Enable(EnableCap.StencilTest) Else GL.Disable(EnableCap.StencilTest)
            If scissor Then GL.Enable(EnableCap.ScissorTest) Else GL.Disable(EnableCap.ScissorTest)
            If discardRaster Then GL.Enable(EnableCap.RasterizerDiscard) Else GL.Disable(EnableCap.RasterizerDiscard)
            If offsetFill Then GL.Enable(EnableCap.PolygonOffsetFill) Else GL.Disable(EnableCap.PolygonOffsetFill)
            GL.PolygonOffset(prevFactor, prevUnits)
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, prevFbo)
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, prevRead)
            GL.Viewport(vp(0), vp(1), vp(2), vp(3))
        End Try
    End Function

#If DEBUG Then
    ' =================================================================================================================================
    ' GLSL (closed list of what GLSL 4.40 / 4.50 / 4.60 add to 4.30) - DEBUG ONLY (ola 9, 5b): a validation of the app's own closed
    ' texts, run by ShaderSource in Debug and by the gates (ParityGate gl43-glsl), never by a Release runtime.
    ' =================================================================================================================================

    ''' <summary>The built-in functions and variables GLSL 4.60.8 has and 4.30.8 does not (measured on the two Khronos
    ''' specifications, ola GL43: every name followed by '(' and every gl_ name of 4.60 minus those of 4.30, prose words dropped).</summary>
    Friend Shared ReadOnly GlslNamesAbove430 As String() = {
        "allInvocations", "allInvocationsEqual", "anyInvocation", "anyInvocationsEqual",
        "atomicCounterAdd", "atomicCounterAnd", "atomicCounterCompSwap", "atomicCounterExchange", "atomicCounterMax", "atomicCounterMin",
        "atomicCounterOr", "atomicCounterSubtract", "atomicCounterXor",
        "dFdxCoarse", "dFdxFine", "dFdyCoarse", "dFdyFine", "fwidthCoarse", "fwidthFine",
        "imageSamples", "textureSamples", "subpassLoad",
        "gl_BaseInstance", "gl_BaseVertex", "gl_CullDistance", "gl_DrawID", "gl_HelperInvocation", "gl_InstanceIndex",
        "gl_MaxCombinedClipAndCullDistances", "gl_MaxCombinedShaderOutputResources", "gl_MaxCullDistances", "gl_MaxInputAttachments",
        "gl_MaxSamples", "gl_MaxTransformFeedbackBuffers", "gl_MaxTransformFeedbackInterleavedComponents", "gl_VertexIndex"}

    ''' <summary>The layout qualifier ids GLSL 4.60.8 has and 4.30.8 does not (same measurement: align, the transform-feedback and
    ''' component ids of 4.40, the SPIR-V / Vulkan ids of 4.60). 'offset' is 4.40 except on an atomic_uint (4.20) and 'location' on a
    ''' block member is 4.40: GlslViolations checks those two in context.</summary>
    Friend Shared ReadOnly GlslLayoutIdsAbove430 As String() = {
        "align", "component", "xfb_buffer", "xfb_offset", "xfb_stride", "push_constant", "set", "constant_id", "input_attachment_index",
        "local_size_x_id", "local_size_y_id", "local_size_z_id"}

    ''' <summary>The GLSL text without its comments (each replaced by blanks, line ends kept: positions and lines stay).</summary>
    Private Shared Function SinComentarios(src As String) As String
        Dim sb As New Text.StringBuilder(src.Length)
        Dim i = 0
        While i < src.Length
            If i + 1 < src.Length AndAlso src(i) = "/"c AndAlso src(i + 1) = "/"c Then
                While i < src.Length AndAlso src(i) <> ChrW(10)
                    sb.Append(" "c) : i += 1
                End While
            ElseIf i + 1 < src.Length AndAlso src(i) = "/"c AndAlso src(i + 1) = "*"c Then
                sb.Append("  ") : i += 2
                While i < src.Length AndAlso Not (src(i) = "*"c AndAlso i + 1 < src.Length AndAlso src(i + 1) = "/"c)
                    sb.Append(If(src(i) = ChrW(10), ChrW(10), " "c)) : i += 1
                End While
                If i < src.Length Then sb.Append("  ") : i += 2
            Else
                sb.Append(src(i)) : i += 1
            End If
        End While
        Return sb.ToString()
    End Function

    Private Shared ReadOnly ReVersion As New Regex("^[ \t]*#[ \t]*version[ \t]+([0-9]+)", RegexOptions.Multiline)
    Private Shared ReadOnly ReExtension As New Regex("^[ \t]*#[ \t]*extension\b", RegexOptions.Multiline)
    Private Shared ReadOnly ReIdent As New Regex("\b[A-Za-z_][A-Za-z0-9_]*\b")
    Private Shared ReadOnly ReLayout As New Regex("\blayout\s*\(([^)]*)\)")
    Private Shared ReadOnly ReLayoutItem As New Regex("^\s*([A-Za-z_][A-Za-z0-9_]*)\s*(?:=\s*(.*?))?\s*$")
    Private Shared ReadOnly ReIntLiteral As New Regex("^(?:0[xX][0-9a-fA-F]+|[0-9]+)[uU]?$")
    Private Shared ReadOnly RePositionWrite As New Regex("\bgl_Position\s*(?:\.\s*[A-Za-z]+\s*|\[[^\]]*\]\s*)?(?:[-+*/]?=)(?!=)")
    Private Shared ReadOnly RePositionWrapped As New Regex("\bgl_Position\s*=\s*gl43ClipToGl\s*\(")
    Private Shared ReadOnly RePerVertexPosition As New Regex("\bgl_(?:in|out)\s*\[[^\]]*\]\s*\.\s*gl_Position\b")
    Private Shared ReadOnly ReUniformBlock As New Regex("\buniform\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{")

    ''' <summary>What in <paramref name="source"/> is above GLSL 4.30 or breaks the clip contract, one line each ("" none):
    ''' a #version above 430; any #extension (none is allowed, user decision); a name of GlslNamesAbove430; a layout id of
    ''' GlslLayoutIdsAbove430, an 'offset' not on an atomic_uint, a 'location' on a block member, a layout value that is not an
    ''' integer literal (constant expressions in layout are 4.40); every write of gl_Position not of the form
    ''' gl_Position = gl43ClipToGl(...); a full shader (with #version) that writes gl_Position without ClipGlsl or ClipGlslCapture; a
    ''' read or write of gl_in[].gl_Position / gl_out[].gl_Position (the conversion is applied once, by the last vertex stage); a
    ''' uniform block other than the clip state's Gl43ClipControl.</summary>
    Friend Shared Function GlslViolations(source As String) As List(Of String)
        Dim v As New List(Of String)
        If source Is Nothing Then Return v
        Dim code = SinComentarios(source)
        Dim lineStarts As New List(Of Integer) From {0}
        For i = 0 To code.Length - 1
            If code(i) = ChrW(10) Then lineStarts.Add(i + 1)
        Next
        Dim lineOf = Function(pos As Integer)
                         Dim k = lineStarts.BinarySearch(pos)
                         Return If(k >= 0, k + 1, (Not k))
                     End Function
        For Each m As Match In ReVersion.Matches(code)
            If Integer.Parse(m.Groups(1).Value) > 430 Then v.Add($"line {lineOf(m.Index)}: #version {m.Groups(1).Value} (above 430)")
        Next
        For Each m As Match In ReExtension.Matches(code)
            v.Add($"line {lineOf(m.Index)}: #extension (no extension is allowed)")
        Next
        Dim names As New HashSet(Of String)(GlslNamesAbove430, StringComparer.Ordinal)
        For Each m As Match In ReIdent.Matches(code)
            If names.Contains(m.Value) Then v.Add($"line {lineOf(m.Index)}: {m.Value} (GLSL above 4.30)")
        Next
        Dim layoutIds As New HashSet(Of String)(GlslLayoutIdsAbove430, StringComparer.Ordinal)
        ' Brace depth at each layout: one walk over the text, the matches in order.
        Dim walked = 0, depth = 0
        For Each m As Match In ReLayout.Matches(code)
            While walked < m.Index
                If code(walked) = "{"c Then depth += 1 Else If code(walked) = "}"c Then depth -= 1
                walked += 1
            End While
            Dim semi = code.IndexOf(";"c, m.Index + m.Length)
            Dim decl = If(semi < 0, code.Substring(m.Index + m.Length), code.Substring(m.Index + m.Length, semi - m.Index - m.Length))
            For Each item In m.Groups(1).Value.Split(","c)
                Dim it = ReLayoutItem.Match(item)
                If Not it.Success Then v.Add($"line {lineOf(m.Index)}: layout item '{item.Trim()}' not read") : Continue For
                Dim id = it.Groups(1).Value
                If layoutIds.Contains(id) Then v.Add($"line {lineOf(m.Index)}: layout({id}) (GLSL above 4.30)")
                If id = "offset" AndAlso Not Regex.IsMatch(decl, "\batomic_uint\b") Then v.Add($"line {lineOf(m.Index)}: layout(offset) on a block member (GLSL 4.40)")
                If id = "location" AndAlso depth > 0 Then v.Add($"line {lineOf(m.Index)}: layout(location) on a block member (GLSL 4.40)")
                If it.Groups(2).Success AndAlso Not ReIntLiteral.IsMatch(it.Groups(2).Value.Trim()) Then
                    v.Add($"line {lineOf(m.Index)}: layout({id} = {it.Groups(2).Value.Trim()}) is not an integer literal (GLSL 4.40)")
                End If
            Next
        Next
        Dim writes = RePositionWrite.Matches(code)
        Dim wrapped = RePositionWrapped.Matches(code).Count
        If writes.Count <> wrapped Then v.Add($"{writes.Count} write(s) of gl_Position, {wrapped} of the form gl_Position = gl43ClipToGl(...)")
        If writes.Count > 0 AndAlso ReVersion.IsMatch(code) AndAlso Not source.Contains(ClipGlsl) AndAlso Not source.Contains(ClipGlslCapture) Then
            v.Add("a shader that writes gl_Position without Gl43.ClipGlsl")
        End If
        For Each m As Match In ReUniformBlock.Matches(code)
            If m.Groups(1).Value <> "Gl43ClipControl" Then v.Add($"line {lineOf(m.Index)}: uniform block {m.Groups(1).Value} (the only uniform block is the clip state's, binding {ClipBinding})")
        Next
        For Each m As Match In RePerVertexPosition.Matches(code)
            v.Add($"line {lineOf(m.Index)}: {m.Value} (the clip conversion is the last vertex stage's)")
        Next
        Return v
    End Function

#End If

    ''' <summary>The Debug check of a shader text every program is built from (ProgramCompiler: compiled, or loaded from the program
    ''' binary cache): a text with GlslViolations is refused (InvalidOperationException) and every text is recorded (GateCompiledTexts,
    ''' rev-B11 / B13: ShadowGate checks at the end of each run that every text it built and no harness declared (GateHarnessText) is
    ''' one the gate evaluates).
    ''' <para>DEBUG ONLY (user decision 13-oct-2026, ola 2b): in Release the shader texts are closed - the library's own constants and
    ''' the generated FO4 stages, nothing the user writes - and the ParityGate gate gl43-glsl runs GlslViolations over every one of
    ''' them before a delivery (Tools\ParityGate\Gl43GlslTexts.vb), so checking them again at every start adds nothing. The
    ''' clip-state check of UseProgram (law gl43-unbound-clip) stays in every build: it guards runtime state, not a text (measured in
    ''' Release: 1.7 us per bind over 24863 binds of --fo4-deferred-scene, 58.7 us over the 2028 of --fo4-gbuffer-scenes, where the
    ''' binding query waits on the GPU).</para></summary>
    <Diagnostics.Conditional("DEBUG")>
    Friend Shared Sub CheckShaderText(source As String)
#If DEBUG Then
        Dim v = GlslViolations(source)
        If v.Count > 0 Then Throw New InvalidOperationException("GLSL above 4.30 or off the GL43 clip contract: " & String.Join("; ", v))
        GateCompiledTexts.TryAdd(GateSha256(source), source.Length)
#End If
    End Sub

    ''' <summary>glShaderSource. Its one caller is ProgramCompiler, which checks every text a program is built from (CheckShaderText)
    ''' before the cache or the compile see it (ParityGate program-compiler-single: no other caller).</summary>
    Friend Shared Sub ShaderSource(shader As Integer, source As String)
        GL.ShaderSource(shader, source)
    End Sub

    ''' <summary>GATE (ola 2b rev-B13): a harness declares, where it produces it, that <paramref name="text"/> is its own (a mutant, an
    ''' oracle, a probe), under the name <paramref name="site"/>; returns the text unchanged. Every compiled text not declared here is
    ''' the app's (ShadowGate's gl43-glsl coverage check: it must be one the gate gl43-glsl evaluates). In Release nothing is
    ''' recorded.</summary>
    Friend Shared Function GateHarnessText(site As String, text As String) As String
#If DEBUG Then
        GateHarnessTexts(GateSha256(text)) = site
#End If
        Return text
    End Function

#If DEBUG Then
    ''' <summary>DEBUG ONLY (rev-B11 / B13): the SHA-256 (UTF-8, uppercase hex) of every text a program was built from in this process
    ''' (CheckShaderText: compiled, or loaded from the program binary cache), with its length.</summary>
    Friend Shared ReadOnly GateCompiledTexts As New ConcurrentDictionary(Of String, Integer)(StringComparer.Ordinal)

    ''' <summary>DEBUG ONLY (rev-B13): the SHA-256 of every text a harness declared (GateHarnessText), with its site.</summary>
    Friend Shared ReadOnly GateHarnessTexts As New ConcurrentDictionary(Of String, String)(StringComparer.Ordinal)

    ''' <summary>DEBUG ONLY: the key of a text in GateCompiledTexts / GateHarnessTexts.</summary>
    Friend Shared Function GateSha256(s As String) As String
        Return Convert.ToHexString(Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s)))
    End Function
#End If
End Class

