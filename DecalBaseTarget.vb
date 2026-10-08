Imports OpenTK.Graphics.OpenGL4

''' <summary>THE BASE OF THE TRANSLUCENT DECALS, BOTH GAMES (user decisions 5-oct-2026 and 7-oct-2026; an app pass, neither engine has
''' one): where nothing opaque is behind a translucent decal (the frame's depth still holds its clear value 1.0), the farthest decal
''' that paints the pixel lays a surface under the decals - albedo = the preview's background at the pixel, normal = that decal's
''' geometric normal, lit as any surface of its game - with that decal's OPACITY (user decision 7-oct-2026; review rev-06:
''' RenderableMesh.DecalBaseOpacity, its alpha for every blended decal whatever its factors, 1 for an unblended one). The base is
''' a premultiplied layer of that opacity over the background and the decals blend over it unchanged: what the post (or the
''' display) mixes with the background is the coverage of base and decals together. Where something is behind, nothing changes.
''' This target is the auxiliary depth/stencil where that base is resolved, plus the stencil law of its draws:
''' <list type="number">
''' <item>Begin: the frame's depth/stencil copied raw into the auxiliary target and into a sampled copy (CopyImageSubData, GL 4.6
''' 18.3.2: "a CPU memcpy"; Fallout 4: the G-buffer's own depth copy, Skyrim SE: this target's); the auxiliary stencil cleared to
''' 0 (GL 4.6 17.4.3); the colour targets the base surface writes
''' attached; mark: Fragment_DecalBase with uOnlyEmpty where the copy holds 1.0 writes depth 0.0 and stencil MarkBit (with the
''' coverage target, Fallout 4: also coverage 0 there, the target being cleared to 1 first).</item>
''' <item>The depth draws, every decal (RenderableMesh.ApplyDecalBaseState: its own program, depth GREATER + write) under
''' DepthDrawStencil: they pass where MarkBit is set and on depth pass set CoveredBit (GL 4.6 17.3.3: the test compares the
''' reference and the stored value both ANDed with the value mask; 17.4.2: the write mask limits the bits a stencil op writes). The
''' farthest painting fragment's depth stays. With the alpha target (Fallout 4), each depth draw also writes its colour output's
''' alpha there (colour mask A only): the value left is the farthest fragment's, the one whose depth stays.</item>
''' <item>The surface draws, every decal again, AFTER all the depth draws (BeginSurfaces; RenderableMesh.DrawDecalBaseSurface)
''' under SurfaceDrawStencil: depth EQUAL against the final base depth (only the farthest decal's fragments pass), stencil
''' CoveredBit set and SurfacedBit clear, SurfacedBit set on pass (GL 4.6 17.3.3, INVERT under write mask SurfacedBit): one surface
''' fragment per pixel. After all the depth draws because a blended surface cannot be overwritten by a farther one. The surface
''' adds its opacity to the coverage (Skyrim SE: the HDR coverage, uCoverageMode 1; Fallout 4: the coverage target, attachment 6).
''' Fallout 4, then the coverage draw of the same decal (CoverageDrawStencil: depth LEQUAL against the base depth, the fragments its
''' colour draw will blend there; marked and covered pixels) into the coverage target, blended ONE / ONE_MINUS_SRC_ALPHA on alpha
''' (RenderableMesh.DrawDecalBaseSurface).</item>
''' <item>Fallout 4 only: Fill (stencil exactly MarkBit = marked and taken by no decal: depth 1.0 back) and the depth blitted back
''' into the G-buffer (Fo4DeferredTargets.EndDecalBase). Skyrim SE keeps its frame depth: its decals do not write depth.</item>
''' </list></summary>
Friend NotInheritable Class DecalBaseTarget
    ''' <summary>Stencil bits of the auxiliary target: marked (nothing behind), taken by a decal's depth draw, surfaced.</summary>
    Private Const MarkBit As Integer = &H1
    Private Const CoveredBit As Integer = &H40
    Private Const SurfacedBit As Integer = &H80

    ''' <summary>True: Fallout 4's deferred base, which needs the alpha target (the base surface is its own program, Fragment_
    ''' DecalBaseSurface, and reads the farthest decal's alpha) and the coverage target (composite 2 takes the coverage from it).</summary>
    Private ReadOnly _deferred As Boolean
    Private _w As Integer, _h As Integer
    ''' <summary>Skyrim SE only: the sampled copy of the frame's depth the mark reads (Fallout 4 passes the G-buffer's, Begin).</summary>
    Private _copy As Integer
    Private _aux As Integer, _fbo As Integer, _emptyVao As Integer, _point As Integer
    Private _alpha As Integer, _coverage As Integer, _fboDepth As Integer, _fboCoverage As Integer
    Private _colourCount As Integer

    ''' <param name="deferred">Fallout 4's G-buffer base (alpha and coverage targets); False for Skyrim SE's forward base.</param>
    Sub New(deferred As Boolean)
        _deferred = deferred
    End Sub

    ''' <summary>GATE ONLY: the auxiliary depth/stencil texture (depth sampled).</summary>
    Friend ReadOnly Property AuxTexture As Integer
        Get
            Return _aux
        End Get
    End Property

    ''' <summary>GATE ONLY: Skyrim SE's raw copy of the frame's depth taken when the base pass began (depth sampled; 0 for Fallout 4,
    ''' whose mark reads the G-buffer's copy).</summary>
    Friend ReadOnly Property GateCopyTexture As Integer
        Get
            Return _copy
        End Get
    End Property

    ''' <summary>Fallout 4: the farthest painting decal's colour-output alpha per pixel (RGBA16 UNORM, .a; D3D rows; 0 before Ensure).</summary>
    Friend ReadOnly Property AlphaTexture As Integer
        Get
            Return _alpha
        End Get
    End Property

    ''' <summary>Fallout 4: the coverage of base and decals (RGBA8, .a; D3D rows): 1 on every pixel the base pass did not mark, the
    ''' accumulated coverage on the marked ones (0 before Ensure). Composite pass 2 reads it.</summary>
    Friend ReadOnly Property CoverageTexture As Integer
        Get
            Return _coverage
        End Get
    End Property

    ''' <summary>GATE ONLY (no UI): ShadowGate --decal-base-surface measurement of the alpha target's format (review rev-04). True: the
    ''' next Ensure that allocates gives the alpha target RGBA32F (the shader's alpha unrounded) instead of RGBA16 UNORM.</summary>
    Friend Shared GateAlphaFloat As Boolean = False

    Private Shared Function DepthTexture(w As Integer, h As Integer) As Integer
        Dim t As Integer
        GL.CreateTextures(TextureTarget.Texture2D, 1, t)
        GL.TextureStorage2D(t, 1, SizedInternalFormat.Depth24Stencil8, w, h)
        GL.TextureParameter(t, TextureParameterName.DepthStencilTextureMode, CInt(All.DepthComponent))
        Return t
    End Function

    Private Shared Function ColourTexture(fmt As SizedInternalFormat, w As Integer, h As Integer) As Integer
        Dim t As Integer
        GL.CreateTextures(TextureTarget.Texture2D, 1, t)
        GL.TextureStorage2D(t, 1, fmt, w, h)
        Return t
    End Function

    ''' <summary>A framebuffer on the auxiliary depth/stencil with one colour texture at attachment 0 (draw buffer 0).</summary>
    Private Function AuxFramebuffer(colour As Integer) As Integer
        Dim f As Integer
        GL.CreateFramebuffers(1, f)
        GL.NamedFramebufferTexture(f, FramebufferAttachment.DepthStencilAttachment, _aux, 0)
        GL.NamedFramebufferTexture(f, FramebufferAttachment.ColorAttachment0, colour, 0)
        GL.NamedFramebufferDrawBuffer(f, DrawBufferMode.ColorAttachment0)
        GL.NamedFramebufferReadBuffer(f, ReadBufferMode.None)
        Return f
    End Function

    ''' <summary>Allocates for w x h (D24S8 like the frames' depth: the raw copy needs the same format, GL 4.6 18.3.2; the FO4 blit
    ''' back too, 18.3.1). Skyrim SE: the auxiliary target and the sampled copy (8 B/px). Fallout 4: the auxiliary target, the alpha
    ''' target RGBA16 UNORM and the coverage target RGBA8 (16 B/px; the sampled copy is the G-buffer's). The alpha target: the depth
    ''' draws write their colour output 0, whose alpha is .w (there is no alpha-only colour format in GL core); UNORM clamps it to
    ''' [0, 1] - the law, the fixed-point RT 0 blend clamps it, GL 4.6 17.3.6 - and converts it to 16 bits (GL 4.6 2.3.5.2: one of
    ''' the two closest values, round to nearest a "should"): an error below 2^-16 against the shader's value, which moves an 8-bit
    ''' result (albedo, coverage) by at most 1 LSB, only where the exact value lies that close to a rounding boundary (review rev-04;
    ''' measured: ShadowGate --decal-base-surface 2h).
    ''' The coverage target RGBA8: the format of the HDR coverage it is copied into by composite 2, the same 8-bit values. False when
    ''' a framebuffer is incomplete.</summary>
    Public Function Ensure(w As Integer, h As Integer) As Boolean
        If w <= 0 OrElse h <= 0 Then Return False
        If _fbo <> 0 AndAlso w = _w AndAlso h = _h Then Return True
        Free()
        If Not _deferred Then _copy = DepthTexture(w, h)
        _aux = DepthTexture(w, h)
        GL.CreateFramebuffers(1, _fbo)
        GL.NamedFramebufferTexture(_fbo, FramebufferAttachment.DepthStencilAttachment, _aux, 0)
        GL.NamedFramebufferDrawBuffer(_fbo, DrawBufferMode.None)
        GL.NamedFramebufferReadBuffer(_fbo, ReadBufferMode.None)
        Dim ok = GL.CheckNamedFramebufferStatus(_fbo, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        If _deferred Then
            _alpha = ColourTexture(If(GateAlphaFloat, SizedInternalFormat.Rgba32f, SizedInternalFormat.Rgba16), w, h)
            _coverage = ColourTexture(SizedInternalFormat.Rgba8, w, h)
            _fboDepth = AuxFramebuffer(_alpha)
            _fboCoverage = AuxFramebuffer(_coverage)
            ok = ok AndAlso GL.CheckNamedFramebufferStatus(_fboDepth, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
            ok = ok AndAlso GL.CheckNamedFramebufferStatus(_fboCoverage, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        End If
        _emptyVao = GL.GenVertexArray()
        _point = GL.GenSampler()
        For Each p In {SamplerParameterName.TextureWrapS, SamplerParameterName.TextureWrapT}
            GL.SamplerParameter(_point, p, CInt(TextureWrapMode.ClampToEdge))
        Next
        GL.SamplerParameter(_point, SamplerParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.SamplerParameter(_point, SamplerParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        If Not ok Then Free() : Return False
        _w = w : _h = h
        Return True
    End Function

    ''' <summary>Opens the base pass (step 1 of the class summary) and leaves the state of the depth draws (step 2).
    ''' <paramref name="srcName"/> / <paramref name="srcTarget"/>: the frame's depth/stencil (FO4: the G-buffer's texture; SSE: the
    ''' scene renderbuffer). <paramref name="depthCopy"/>: Fallout 4, the sampled D24S8 texture the frame's depth is copied into for
    ''' the mark - the G-buffer's own copy (Fo4DeferredTargets._depthCopy, which EndGBuffer takes again for the lights): one copy per
    ''' frame, not two (review rev-03); Skyrim SE passes 0 and this target's copy is used. <paramref name="colours"/>: the targets the
    ''' surface draws write, in draw-buffer order (renderbuffer when
    ''' IsRenderbuffer; Fallout 4 adds the coverage target after them). <paramref name="nearestMutant"/>: GATE ONLY (Fo4DeferredTargets.GateDecalBaseNearest), the mark writes 1.0.
    ''' False when the target with those colours is incomplete (nothing is bound or drawn then).</summary>
    Public Function Begin(mark As Shader_Base_Class, srcName As Integer, srcTarget As ImageTarget, depthCopy As Integer,
                          colours As (Name As Integer, IsRenderbuffer As Boolean)(), nearestMutant As Boolean) As Boolean
        Dim copy = If(_deferred, depthCopy, _copy)
        GL.CopyImageSubData(srcName, srcTarget, 0, 0, 0, 0, copy, ImageTarget.Texture2D, 0, 0, 0, 0, _w, _h, 1)
        GL.CopyImageSubData(srcName, srcTarget, 0, 0, 0, 0, _aux, ImageTarget.Texture2D, 0, 0, 0, 0, _w, _h, 1)
        For i = 0 To colours.Length - 1
            Dim att = CType(FramebufferAttachment.ColorAttachment0 + i, FramebufferAttachment)
            If colours(i).IsRenderbuffer Then
                GL.NamedFramebufferRenderbuffer(_fbo, att, RenderbufferTarget.Renderbuffer, colours(i).Name)
            Else
                GL.NamedFramebufferTexture(_fbo, att, colours(i).Name, 0)
            End If
        Next
        ' Fallout 4: the coverage target after the caller's colours (Fragment_DecalBaseSurface oCoverage, location 6).
        Dim count = colours.Length
        If _deferred Then
            GL.NamedFramebufferTexture(_fbo, CType(FramebufferAttachment.ColorAttachment0 + count, FramebufferAttachment), _coverage, 0)
            count += 1
        End If
        For i = count To _colourCount - 1
            GL.NamedFramebufferTexture(_fbo, CType(FramebufferAttachment.ColorAttachment0 + i, FramebufferAttachment), 0, 0)
        Next
        _colourCount = count
        GL.NamedFramebufferDrawBuffers(_fbo, count,
                                       Enumerable.Range(0, count).Select(Function(i) CType(DrawBuffersEnum.ColorAttachment0 + i, DrawBuffersEnum)).ToArray())
        If GL.CheckNamedFramebufferStatus(_fbo, FramebufferTarget.Framebuffer) <> FramebufferStatus.FramebufferComplete Then Return False
        GL.DepthMask(True)
        GL.StencilMask(&HFF)
        GL.ClearNamedFramebuffer(_fbo, ClearBuffer.Stencil, 0, New Integer() {0})
        GL.Viewport(0, 0, _w, _h)
        GL.Disable(EnableCap.Blend)
        GL.ColorMask(False, False, False, False)
        If _deferred Then
            ' The coverage target: 1 everywhere (the pixels the pass does not mark keep coverage 1 in composite 2), then the mark
            ' writes its colour output 0 (Fragment_DecalBase oCoverage) on the marked pixels, where the coverage draws accumulate.
            GL.ColorMask(0, True, True, True, True)
            GL.ClearNamedFramebuffer(_fboCoverage, ClearBuffer.Color, 0, New Single() {1.0F, 1.0F, 1.0F, 1.0F})
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboCoverage)
        Else
            ' The mark writes no colour into the frame's targets (masked).
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        End If
        GL.Enable(EnableCap.StencilTest)
        GL.StencilFunc(StencilFunction.Always, MarkBit, &HFF)
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace)
        mark.Use()
        mark.SetBool("uOnlyEmpty", True)
        mark.SetFloat("uDepth", If(nearestMutant, 1.0F, 0.0F))
        GL.BindTextureUnit(0, copy) : GL.BindSampler(0, _point)
        FullscreenPass.Draw(mark, _emptyVao, 1, writesDepth:=True)
        GL.BindSampler(0, 0)
        GL.ColorMask(False, False, False, False)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, If(_deferred, _fboDepth, _fbo))
        DepthDrawStencil()
        Return True
    End Function

    ''' <summary>The stencil state of a decal's depth draw (step 2): "MarkBit &lt;= stored AND MarkBit" passes exactly on the marked
    ''' pixels; on depth pass the reference's CoveredBit is written, the write mask keeping every other bit.</summary>
    Friend Shared Sub DepthDrawStencil()
        GL.StencilFunc(StencilFunction.Lequal, MarkBit Or CoveredBit, MarkBit)
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace)
        GL.StencilMask(CoveredBit)
    End Sub

    ''' <summary>Fallout 4, inside a depth draw (step 2): the alpha target takes the draw's colour output alpha (attachment 0, mask A).</summary>
    Friend Sub DepthDrawAlpha()
        If _deferred Then GL.ColorMask(0, False, False, False, True)
    End Sub

    ''' <summary>Fallout 4, the surface draw: runs <paramref name="draw"/> with the alpha target bound to TEXTURE_2D of texture unit
    ''' <paramref name="unit"/> (Fragment_DecalBaseSurface tBaseAlpha, read by texelFetch: no sampler state), then that unit's previous
    ''' TEXTURE_2D binding and the active unit back - the coverage draw that follows samples, with the decal's own program, the units
    ''' ApplyMaterial bound.</summary>
    Friend Sub WithAlphaBound(unit As Integer, draw As Action)
        Dim active = GL.GetInteger(GetPName.ActiveTexture)
        GL.ActiveTexture(CType(TextureUnit.Texture0 + unit, TextureUnit))
        Dim prev = GL.GetInteger(GetPName.TextureBinding2D)
        GL.BindTexture(TextureTarget.Texture2D, _alpha)
        Try
            draw()
        Finally
            GL.BindTexture(TextureTarget.Texture2D, prev)
            GL.ActiveTexture(CType(active, TextureUnit))
        End Try
    End Sub

    ''' <summary>Step 3 begins: the surface draws' framebuffer (the auxiliary depth/stencil and the caller's colour targets).</summary>
    Public Sub BeginSurfaces()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.Viewport(0, 0, _w, _h)
    End Sub

    ''' <summary>The state of a decal's surface draw (step 3), in the surface draws' framebuffer: colour writes on, depth EQUAL without
    ''' writing (the depth draws left the farthest surviving fragment's depth, rasterised identically: GL 4.6 Appendix A rule 2, GLSL
    ''' 4.60 4.8.1 invariant gl_Position), stencil "CoveredBit set and SurfacedBit clear" (value mask both bits), SurfacedBit set on pass
    ''' (INVERT under write mask SurfacedBit; one surface fragment per pixel). The blend is the caller's.</summary>
    Friend Sub SurfaceDrawStencil()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.ColorMask(True, True, True, True)
        GL.DepthFunc(DepthFunction.Equal)
        GL.DepthMask(False)
        GL.StencilFunc(StencilFunction.Equal, CoveredBit, CoveredBit Or SurfacedBit)
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Invert)
        GL.StencilMask(SurfacedBit)
    End Sub

    ''' <summary>Fallout 4, the coverage draw of a decal (step 3): the coverage target bound, alpha writes only, depth LEQUAL without
    ''' writing against the base depth (the test its colour draw passes there: the fragments it will blend), stencil CoveredBit set
    ''' (marked and taken), kept. The blend is the caller's (RenderableMesh.DrawDecalBaseSurface: the decal's coverage law).</summary>
    Friend Sub CoverageDrawStencil()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboCoverage)
        GL.ColorMask(False, False, False, False)
        GL.ColorMask(0, False, False, False, True)
        GL.DepthFunc(DepthFunction.Lequal)
        GL.DepthMask(False)
        GL.StencilFunc(StencilFunction.Equal, CoveredBit, CoveredBit)
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep)
        GL.StencilMask(0)
    End Sub

    ''' <summary>Fallout 4 (step 4): the marked pixels no decal took (stencil exactly MarkBit) get the clear depth 1.0 back.</summary>
    Public Sub Fill(mark As Shader_Base_Class)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.ColorMask(False, False, False, False)
        GL.Disable(EnableCap.Blend)
        GL.DepthMask(True)
        GL.StencilFunc(StencilFunction.Equal, MarkBit, &HFF)
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep)
        mark.Use()
        mark.SetBool("uOnlyEmpty", False)
        mark.SetFloat("uDepth", 1.0F)
        FullscreenPass.Draw(mark, _emptyVao, 0, writesDepth:=True)
    End Sub

    ''' <summary>Closes the base pass: stencil test off and its write mask back to all bits (a later clear honours it, GL 4.6 17.4.3),
    ''' blend off, colour and depth writes on, LESS_EQUAL. The caller binds its frame target again.</summary>
    Public Sub Close()
        GL.Disable(EnableCap.StencilTest)
        GL.StencilMask(&HFF)
        GL.Disable(EnableCap.Blend)
        GL.ColorMask(True, True, True, True)
        GL.DepthMask(True)
        GL.DepthFunc(DepthFunction.Lequal)
    End Sub

    ''' <summary>Fallout 4 (step 4): the auxiliary depth blitted into <paramref name="dstFbo"/>, DEPTH only, NEAREST, same rectangle and
    ''' format (GL 4.6 18.3.1; "Color, depth, and stencil masks ... are ignored"); the destination stencil is not in the mask.</summary>
    Public Sub BlitDepthTo(dstFbo As Integer)
        GL.BlitNamedFramebuffer(_fbo, dstFbo, 0, 0, _w, _h, 0, 0, _w, _h, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest)
    End Sub

    Public Sub Free()
        For Each f In {_fbo, _fboDepth, _fboCoverage}
            If f <> 0 Then GL.DeleteFramebuffer(f)
        Next
        For Each t In {_copy, _aux, _alpha, _coverage}
            If t <> 0 Then GL.DeleteTexture(t)
        Next
        If _emptyVao <> 0 Then GL.DeleteVertexArray(_emptyVao)
        If _point <> 0 Then GL.DeleteSampler(_point)
        _fbo = 0 : _fboDepth = 0 : _fboCoverage = 0 : _copy = 0 : _aux = 0 : _alpha = 0 : _coverage = 0
        _emptyVao = 0 : _point = 0 : _colourCount = 0
        _w = 0 : _h = 0
    End Sub
End Class

''' <summary>Skyrim SE's instance of the mark/fill program of the base (Fo4DeferredAppSource.Fragment_DecalBase; Fallout 4 builds its
''' own in Fo4DeferredPrograms): one program set per game, so that one game's build failure leaves the other its frame (rev-50).</summary>
Public Class DecalBase_Mark_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4DeferredAppSource.Fragment_DecalBase)
    End Sub
End Class
