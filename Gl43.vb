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
    ''' rasterizer discard): the captured gl_Position is the D3D clip position the VS writes; the draw that reads it (Vertex_FO4)
    ''' converts it with ClipGlsl, as the host's own draw does.</summary>
    Friend Const ClipGlslCapture As String = "// GL43 CLIP CONTROL, TRANSFORM-FEEDBACK CAPTURE (rasterizer discard): gl_Position keeps the D3D clip position.
vec4 gl43ClipToGl(vec4 p) { return p; }
"

    Private NotInheritable Class ClipState
        Public Ubo As Integer
        Public Origin As ClipOrigin
        Public RequestedFront As FrontFaceDirection
    End Class

    Private Shared ReadOnly _clip As New ConcurrentDictionary(Of IntPtr, ClipState)()

    <DllImport("opengl32.dll", ExactSpelling:=True)>
    Private Shared Function wglGetCurrentContext() As IntPtr
    End Function

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
        Dim prev = GL.GetInteger(GetPName.UniformBufferBinding)
        GL.BindBuffer(BufferTarget.UniformBuffer, st.Ubo)
        GL.BufferData(BufferTarget.UniformBuffer, 16, New Single() {1.0F, 0.0F, 0.0F, 0.0F}, BufferUsageHint.DynamicDraw)
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, ClipBinding, st.Ubo)
        GL.BindBuffer(BufferTarget.UniformBuffer, prev)
        _clip(ctx) = st
        GL.FrontFace(FrontFaceDirection.Ccw)
        Return ctx
    End Function

    ''' <summary>Forgets the clip state of context <paramref name="ctx"/> (its teardown: the context's destruction frees the
    ''' buffer; no GL call, so it is safe whatever context is current).</summary>
    Friend Shared Sub ReleaseContext(ctx As IntPtr)
        Dim st As ClipState = Nothing
        _clip.TryRemove(ctx, st)
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
        If program <> 0 Then
            Dim st = Current()
            Dim bound As Integer
            GL.GetInteger(GetIndexedPName.UniformBufferBinding, ClipBinding, bound)
            If bound <> st.Ubo Then Throw New InvalidOperationException($"Gl43.UseProgram: uniform buffer binding {ClipBinding} holds {bound}, not the clip state's {st.Ubo}")
        End If
        GL.UseProgram(program)
    End Sub

    ''' <summary>GATE ONLY (ShadowGate --fo4-deferred-scene, law gl43-unbound-clip): <paramref name="attached"/> False detaches the
    ''' clip buffer from ClipBinding, True puts it back.</summary>
    Friend Shared Sub GateClipBufferAttached(attached As Boolean)
        Dim st = Current()
        Dim prev = GL.GetInteger(GetPName.UniformBufferBinding)
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, ClipBinding, If(attached, st.Ubo, 0))
        GL.BindBuffer(BufferTarget.UniformBuffer, prev)
    End Sub

    ''' <summary>GATE / CHECK: the current context's clip uniform buffer (the one at ClipBinding): what every vertex stage reads.</summary>
    Friend Shared ReadOnly Property ClipBuffer As Integer
        Get
            Return Current().Ubo
        End Get
    End Property

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
    ' GLSL (closed list of what GLSL 4.40 / 4.50 / 4.60 add to 4.30)
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

    ''' <summary>glShaderSource with the GL 4.3 check: a source with GlslViolations is not compiled (InvalidOperationException).</summary>
    Friend Shared Sub ShaderSource(shader As Integer, source As String)
        Dim v = GlslViolations(source)
        If v.Count > 0 Then Throw New InvalidOperationException("GLSL above 4.30 or off the GL43 clip contract: " & String.Join("; ", v))
        GL.ShaderSource(shader, source)
    End Sub
End Class
