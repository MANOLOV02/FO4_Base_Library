Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

''' <summary>The app's passes of Fallout 4's post (Fo4ImageSpaceChain) that are not transcribed shaders. ASCII only (GLSL).</summary>
Friend Module Fo4ImageSpaceAppSource
    ''' <summary>THE ENGINE'S SCENE FROM THE PREVIEW'S (decisions of the PLAN, ola 4, P3 / P5 of D:\WinTmp\rc\ola4\post\post.md 6): the
    ''' scene target holds the radiance premultiplied by the composite's coverage (r) and the actor's coverage (g) (SceneTargets,
    ''' rev-19); the engine's RT 1 is the radiance itself. o0 = RT 1 = radiance / coverage (0 where nothing was drawn: the background
    ''' is UI, composited after the post); o1 = RT 1 x the actor's coverage and o2 = the actor's coverage (g, g, g): the two inputs of
    ''' the luminance pyramids the mean of the actor's pixels is the ratio of (decision P3: "only the actor, with the engine's box
    ''' pyramid"). Every pixel of the target is written (texelFetch at gl_FragCoord: the same texel whatever the clip origin).</summary>
    Friend Const Fragment_Unpremultiply As String = "#version 430
layout(binding = 0) uniform sampler2D texScene;
layout(binding = 1) uniform sampler2D texCoverage;
layout(location = 0) out vec4 oScene;
layout(location = 1) out vec4 oActor;
layout(location = 2) out vec4 oWeight;
void main()
{
    ivec2 p = ivec2(gl_FragCoord.xy);
    vec4 cov = texelFetch(texCoverage, p, 0);
    vec3 c = cov.r > 0.0 ? texelFetch(texScene, p, 0).rgb / cov.r : vec3(0.0);
    oScene = vec4(c, 1.0);
    oActor = vec4(c * cov.g, 1.0);
    oWeight = vec4(vec3(cov.g), 1.0);
}
"

    ''' <summary>THE MEAN LUMINANCE OF THE ACTOR'S PIXELS (decision P3, app): the 1 x 1 level of the actor's radiance pyramid over the
    ''' 1 x 1 level of its coverage pyramid (both the engine's box pyramid, Fo4ImageSpaceChain.RunPyramid); 0 without an actor pixel
    ''' (the exposure law then takes it to AutoExposureMax: MiddleGray / (0 + 0.001) clamped). Written where the engine's eye
    ''' adaptation leaves its result (the tonemap's t2 Avg, the bright pass's t1), in that level's format.</summary>
    Friend Const Fragment_ActorMean As String = "#version 430
layout(binding = 0) uniform sampler2D texActor;
layout(binding = 1) uniform sampler2D texWeight;
layout(location = 0) out vec4 oMean;
void main()
{
    float a = texelFetch(texActor, ivec2(0), 0).x;
    float w = texelFetch(texWeight, ivec2(0), 0).x;
    oMean = vec4(w > 0.0 ? a / w : 0.0);
}
"

    ''' <summary>THE PREVIEW'S COMPOSITE (declared, not engine: the background is UI): the presented image of the post - GammaCorrectLUT's
    ''' output, display values in an RGBA8 target as the engine's presentation RT (post.md 2.6) - mixed over the preview's background
    ''' (with the ground catcher's factor, target 2 of SceneTargets) by the composite's coverage, as Fragment_PostFo4 composited.</summary>
    Friend Const Fragment_Present As String = "#version 430
out vec4 FragColor;
" & BackgroundFadeSource.Fade_Helper & "
layout(binding = 0) uniform sampler2D texPresent;
layout(binding = 1) uniform sampler2D texCoverage;
layout(binding = 2) uniform sampler2D texBgShadow;
void main()
{
    ivec2 p = ivec2(gl_FragCoord.xy);
    float cov = texelFetch(texCoverage, p, 0).r;
    vec3 bg = backgroundAt(gl_FragCoord.xy) * texelFetch(texBgShadow, p, 0).rgb;
    vec3 o = texelFetch(texPresent, p, 0).rgb;
    FragColor = vec4(cov <= 0.0 ? bg : mix(bg, o, cov), 1.0);
}
"
End Module

''' <summary>FALLOUT 4'S POST OF THE WORLD (ola 4; D:\WinTmp\rc\ola4\post\post.md 2): the ImageSpaceManager's effects the default chain
''' runs (0x1421FA200: range 0..13, then 15..21) with the exe's Settings and no IMAD, on the transcribed image-space programs
''' (Fo4ImageSpaceSource) - in order:
''' <list type="number">
''' <item>the engine's scene from the preview's (Fo4ImageSpaceAppSource.Fragment_Unpremultiply);</item>
''' <item>HDR (effect 3, ImageSpaceEffectHDR::Render 0x142209D70): HDRDownSample4 (i3706) of the scene into RT 0x46; the luminance
''' pyramid RGB2Lum (i3710) / 16Lum (i3708) to 1 x 1 - in the steady state of the PLAN's decision (the eye adaptation's reset form,
''' 0x14220A26B..274: the last level is 16Lum's, no 16LightAdapt) and over the actor's pixels (decision P3: two pyramids and their
''' ratio, Fragment_ActorMean); the bloom (effect 43, radius 7, BrightPassBlur15 i3768 then Blur15 i3736); the tonemap
''' HDRTonemapBlendCinematic (i3700);</item>
''' <item>ISRefraction (effect 5, i3658) when the frame drew refraction normals;</item>
''' <item>GammaCorrectLUT (effect 15, i3648) into the presentation target (RGBA8);</item>
''' <item>the preview's composite over the background (Fragment_Present).</item>
''' </list>
''' Not run (declared): MotionBlur (effect 10: the identity with velocity 0, decision of the PLAN), TemporalAA (effect 18: decision
''' of the PLAN, P1), the eye adaptation's history (steady state). Every image-space pass draws the ImageSpaceManager's triangle
''' (ISM + 0x28: (-1, 1, 0) uv (0, 0), (-1, -3, 0) uv (0, 2), (3, 1, 0) uv (2, 0); 0x14218D4DD..5B6, chosen by 0x1421856EC..703)
''' under UPPER_LEFT: storage row r of each input maps to storage row r of each output, whatever the frame's row order.</summary>
Friend NotInheritable Class Fo4ImageSpaceChain
    Private _w As Integer, _h As Integer
    Private _vao, _vbo As Integer
    Private _point, _linear As Integer
    ''' <summary>RT 1 (W x H R11G11B10, 0x142233F0A), the actor's radiance and coverage (the pyramids' inputs), the tonemap's output, the
    ''' refraction's output, the presentation target (RGBA8).</summary>
    Private _rt1, _actor, _weight, _tone, _refracted, _present As Integer
    ''' <summary>RT 0x46 (ceil(W / 4) x ceil(H / 4) R11G11B10, 0x142234CD5..D5E): HDRDownSample4 of the scene, the bloom's input.</summary>
    Private _rt46 As Integer, _rt46Size As (W As Integer, H As Integer)
    ''' <summary>The two pyramids (actor radiance, actor coverage): level 0 = their HDRDownSample4 (RT 0x46's size), then ceil(/ 4) to
    ''' 1 x 1 (0x14220A080..0F5), R11G11B10 each.</summary>
    Private ReadOnly _pyrActor As New List(Of (Tex As Integer, W As Integer, H As Integer))
    Private ReadOnly _pyrWeight As New List(Of (Tex As Integer, W As Integer, H As Integer))
    ''' <summary>The 1 x 1 mean (the eye adaptation's result: the tonemap's t2, the bright pass's t1).</summary>
    Private _avg As Integer
    ''' <summary>RT 0x44 / 0x45 ((W &gt;&gt; 2) x (H &gt;&gt; 2) R11G11B10, 0x142234190..1EC): the bloom's vertical and horizontal passes.</summary>
    Private _rt44, _rt45 As Integer
    Private _fbo As Integer
    ''' <summary>The LUT of the image space (SceneTargets.LutVolume) and the identity LUT the engine binds for an empty slot ([0x143D872C8],
    ''' 0x14183F500: 16^3 RGBA8, r, g, b = 17 k, a = 255).</summary>
    Private _lut As Integer, _lutPath As String, _identityLut As Integer
    ''' <summary>The tonemap's t3 while H5 is open: one texel (0, 0, 0, 0) - alpha 0 is no id 4, no pixel bypasses (see Run).</summary>
    Private _noBypass As Integer

    ''' <summary>The four taps of HDRDownSample4 / RGB2Lum (0x142F98300: (+-1, +-1) x texel, weight 0.25, static) and the sixteen of
    ''' 16Lum (0x142F98340: {-1.5, -0.5, 0.5, 1.5}^2, 0.0625), read from the exe's .data.</summary>
    Private Shared ReadOnly Offsets4 As Vector4() = {New Vector4(-1.0F, -1.0F, 0.25F, 0.0F), New Vector4(1.0F, -1.0F, 0.25F, 0.0F),
                                                     New Vector4(1.0F, 1.0F, 0.25F, 0.0F), New Vector4(-1.0F, 1.0F, 0.25F, 0.0F)}
    Private Shared ReadOnly Offsets16 As Vector4() = Enumerable.Range(0, 16).Select(
        Function(i) New Vector4(-1.5F + (i Mod 4), -1.5F + (i \ 4), 0.0625F, 0.0F)).ToArray()

    ''' <summary>The bloom's 15 weights: row 7 of the Gaussian table 0x142F98CF0 (sigma 3.5 texels; the static initializer 0x1401DE380,
    ''' emulated: D:\WinTmp\rc\ola4\v1\scripts\emu_kernel_bits.py) as 0x1422351F0 takes it with radius 7 (row 6 + (row 7 - row 6) x
    ''' 1.0 = row 7 bit for bit), the exact float bits.</summary>
    Private Shared ReadOnly BloomWeightBits As UInteger() = {&H3C827C32UI, &H3CDDD244UI, &H3D2DC3F7UI, &H3D7AE64BUI, &H3DA6F002UI,
        &H3DCCBB66UI, &H3DE7668BUI, &H3DF10A7FUI, &H3DE7668BUI, &H3DCCBB66UI, &H3DA6F002UI, &H3D7AE64BUI, &H3D2DC3F7UI,
        &H3CDDD244UI, &H3C827C32UI}

    ''' <summary>The rows of the bloom's offset tables (0x1422351F0 over the size of input 2, RT 0x44, 0x14223523A): tap i of 0..14 =
    ''' (float(i) - 7.0) x (1.0 / size) in float32, with its weight; vertical (0x143E71950) = (0, d, w, 0), horizontal (0x143E71860) =
    ''' (d, 0, w, 0) (the .w never written: the .bss 0).</summary>
    Friend Shared Function BloomOffsets(size As Integer, vertical As Boolean) As Vector4()
        Dim inv As Single = 1.0F / CSng(size)
        Dim r(14) As Vector4
        For i = 0 To 14
            Dim d As Single = (CSng(i) - 7.0F) * inv
            Dim w = BitConverter.UInt32BitsToSingle(BloomWeightBits(i))
#If DEBUG Then
            ' ShadowGate --fo4-post-law's mutants (Fo4GBufferConstants.GateMutantOn, the one switch).
            If Fo4GBufferConstants.GateMutantOn("post-offsets-no7") Then d = CSng(i) * inv
            If Fo4GBufferConstants.GateMutantOn("post-bloom-row6") Then w = GateRow6Weight(i)
#End If
            r(i) = If(vertical, New Vector4(0.0F, d, w, 0.0F), New Vector4(d, 0.0F, w, 0.0F))
        Next
        Return r
    End Function

    Private Shared Function Tex(fmt As SizedInternalFormat, w As Integer, h As Integer) As Integer
        Dim t = Gl43.CreateTexture(TextureTarget.Texture2D)
        Gl43.TexStorage2D(t, TextureTarget2d.Texture2D, 1, fmt, w, h)
        Return t
    End Function

    ''' <summary>Ceil(n / 4), at least 1 (0x14220A08E..0C1: sar 2, + 1 when n AND 3, min 1).</summary>
    Private Shared Function Quarter(n As Integer) As Integer
        Return Math.Max((n + 3) \ 4, 1)
    End Function

    Public Sub Ensure(w As Integer, h As Integer)
        If _fbo <> 0 AndAlso w = _w AndAlso h = _h Then Return
        Free()
        _rt1 = Tex(SizedInternalFormat.R11fG11fB10f, w, h)
        _actor = Tex(SizedInternalFormat.R11fG11fB10f, w, h)
        _weight = Tex(SizedInternalFormat.R11fG11fB10f, w, h)
        _tone = Tex(SizedInternalFormat.R11fG11fB10f, w, h)
        _refracted = Tex(SizedInternalFormat.R11fG11fB10f, w, h)
        _present = Tex(SizedInternalFormat.Rgba8, w, h)
        _rt46Size = (Quarter(w), Quarter(h))
        _rt46 = Tex(SizedInternalFormat.R11fG11fB10f, _rt46Size.W, _rt46Size.H)
        For Each pyr In {_pyrActor, _pyrWeight}
            Dim lw = _rt46Size.W, lh = _rt46Size.H
            pyr.Add((Tex(SizedInternalFormat.R11fG11fB10f, lw, lh), lw, lh))
            While lw > 1 OrElse lh > 1
                lw = Quarter(lw) : lh = Quarter(lh)
                pyr.Add((Tex(SizedInternalFormat.R11fG11fB10f, lw, lh), lw, lh))
            End While
        Next
        _avg = Tex(SizedInternalFormat.R11fG11fB10f, 1, 1)
        _rt44 = Tex(SizedInternalFormat.R11fG11fB10f, Math.Max(w >> 2, 1), Math.Max(h >> 2, 1))
        _rt45 = Tex(SizedInternalFormat.R11fG11fB10f, Math.Max(w >> 2, 1), Math.Max(h >> 2, 1))
        _fbo = Gl43.CreateFramebuffer()
        _noBypass = Tex(SizedInternalFormat.Rgba8, 1, 1)
        Gl43.TexSubImage2D(_noBypass, TextureTarget.Texture2D, 0, 0, 0, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, New Byte() {0, 0, 0, 0})
        EnsureGeometry()
        _w = w : _h = h
    End Sub

    ''' <summary>The context objects every image-space pass uses, whatever the frame's size: the ImageSpaceManager's triangle and the
    ''' two samplers (created once, released by FreeAll).</summary>
    Private Sub EnsureGeometry()
        If _vao <> 0 Then Return
        ' The ImageSpaceManager's triangle (position xyz, uv).
        _vao = GL.GenVertexArray() : _vbo = GL.GenBuffer()
        GL.BindVertexArray(_vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo)
        Dim tri = {-1.0F, 1.0F, 0.0F, 0.0F, 0.0F, -1.0F, -3.0F, 0.0F, 0.0F, 2.0F, 3.0F, 1.0F, 0.0F, 2.0F, 0.0F}
        GL.BufferData(BufferTarget.ArrayBuffer, tri.Length * 4, tri, BufferUsageHint.StaticDraw)
        GL.EnableVertexAttribArray(0) : GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, False, 20, 0)
        GL.EnableVertexAttribArray(1) : GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, False, 20, 12)
        GL.BindVertexArray(0) : GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        ' FILT 0 / FILT 1 with ADDR 0 (CLAMP): POINT and MIN_MAG_LINEAR_MIP_POINT on single-level targets.
        _point = GL.GenSampler() : _linear = GL.GenSampler()
        For Each s In {_point, _linear}
            For Each p In {SamplerParameterName.TextureWrapS, SamplerParameterName.TextureWrapT, SamplerParameterName.TextureWrapR}
                GL.SamplerParameter(s, p, CInt(TextureWrapMode.ClampToEdge))
            Next
        Next
        GL.SamplerParameter(_point, SamplerParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.SamplerParameter(_point, SamplerParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        GL.SamplerParameter(_linear, SamplerParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        GL.SamplerParameter(_linear, SamplerParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
    End Sub

    ''' <summary>The ImageSpaceManager's triangle (ISRefraction of a direct frame draws it too, RefractionTargets).</summary>
    Friend ReadOnly Property TriangleVao As Integer
        Get
            EnsureGeometry()
            Return _vao
        End Get
    End Property

    ''' <summary>POINT (FILT 0, ADDR 0): the sampler the ImageSpaceManager gives an effect's input 1 (0x1421856C1 / 0x1421856D6).</summary>
    Friend ReadOnly Property PointSampler As Integer
        Get
            EnsureGeometry()
            Return _point
        End Get
    End Property

    ''' <summary>LINEAR (FILT 1, ADDR 0): the sampler of an input passed with +0x1C = 1 (ISRefraction's scene, 0x142255B70).</summary>
    Friend ReadOnly Property LinearSampler As Integer
        Get
            EnsureGeometry()
            Return _linear
        End Get
    End Property

    ''' <summary>Draws image-space program <paramref name="p"/> into <paramref name="target"/> (w x h, colour attachment 0) with the
    ''' textures <paramref name="inputs"/> (unit, texture, linear filter), the ImageSpaceManager's triangle, no depth, no blend.</summary>
    Private Sub Pass(p As Shader_Base_Class, target As Integer, w As Integer, h As Integer, inputs As (Unit As Integer, Texture As Integer, Linear As Boolean)())
        Gl43.FramebufferTexture(_fbo, FramebufferAttachment.ColorAttachment0, target, 0)
        Gl43.DrawBuffer(_fbo, DrawBufferMode.ColorAttachment0)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.Viewport(0, 0, w, h)
        For Each i In inputs
            Gl43.BindTextureUnit(i.Unit, TextureTarget.Texture2D, i.Texture)
            GL.BindSampler(i.Unit, If(i.Linear, _linear, _point))
        Next
        GL.Disable(EnableCap.DepthTest) : GL.DepthMask(False) : GL.Disable(EnableCap.Blend) : GL.Disable(EnableCap.CullFace)
        GL.ColorMask(True, True, True, True)
        GL.BindVertexArray(_vao)
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3)
        GL.BindVertexArray(0)
#If DEBUG Then
        GatePassDone?.Invoke(p, target, w, h, inputs)
#End If
        For Each i In inputs : Gl43.UnbindTextureUnit(i.Unit) : GL.BindSampler(i.Unit, 0) : Next
    End Sub

#If DEBUG Then
    ''' <summary>DEBUG ONLY (ShadowGate --fo4-post-law): called after each pass's draw with its program, target, size and inputs, the
    ''' inputs still bound - the gate's observer of the chain. Nothing in the app.</summary>
    Friend Shared GatePassDone As Action(Of Shader_Base_Class, Integer, Integer, Integer, (Unit As Integer, Texture As Integer, Linear As Boolean)())

    ''' <summary>DEBUG ONLY, the mutant "post-bloom-row6" of ShadowGate --fo4-post-law: tap <paramref name="i"/> of row 6 of the static
    ''' initializer 0x1401DE380 (radius 6: 1 / 7.298055 (0x142F98CD4) x the constants 0x1429176A8 / B0 / C0 / CC / D8 / E8, the centre 1,
    ''' taps 0 and 14 zero).</summary>
    Private Shared Function GateRow6Weight(i As Integer) As Single
        Dim c = {1.0F, BitConverter.UInt32BitsToSingle(&H3F722A5EUI), BitConverter.UInt32BitsToSingle(&H3F4CFD1AUI), BitConverter.UInt32BitsToSingle(&H3F1B459EUI),
                 BitConverter.UInt32BitsToSingle(&H3ED27D46UI), BitConverter.UInt32BitsToSingle(&H3E7F5621UI), BitConverter.UInt32BitsToSingle(&H3E0A9542UI)}
        Dim k = Math.Abs(i - 7)
        If k = 7 Then Return 0.0F
        Dim s As Single = 1.0F / BitConverter.UInt32BitsToSingle(&H40E989ABUI)
        Return If(k = 0, s, s * c(k))
    End Function
#End If

    ''' <summary>HDRDownSample4 (i3705 / i3706, the HDR's sub-slot 4; 0x142209F93..A045): <paramref name="src"/> (the scene's size, FILT 1)
    ''' into <paramref name="dst"/> (RT 0x46's size), BlurScale = (1 / W, 1 / H, 0, 0) and DynamicResolutionParam = (1, 1, 1 - 0.5 /
    ''' W, 1 - 0.5 / H) of the scene (0x142209FED / 0x14220A034; the dynamic resolution [0x142F4E708 / 70C] = 1.0), the 4 taps.</summary>
    Private Sub DownSample4(progs As Fo4DeferredPrograms, src As Integer, dst As Integer)
        Dim p = progs.ImageSpaceProgram(3706UI)
        p.Use()
        p.SetVector4("BlurScale", New Vector4(1.0F / _w, 1.0F / _h, 0.0F, 0.0F))
        p.SetVector4("DynamicResolutionParam", New Vector4(1.0F, 1.0F, 1.0F - 0.5F / _w, 1.0F - 0.5F / _h))
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("post-drp-zw1") Then p.SetVector4("DynamicResolutionParam", Vector4.One)
#End If
        p.SetVector4Array("BlurOffsets", Offsets4)
        Pass(p, dst, _rt46Size.W, _rt46Size.H, {(0, src, True)})
    End Sub

    ''' <summary>One luminance pyramid from its level 0 (0x14220A080..263, the no-history form): each next level ceil(/ 4) of the previous,
    ''' RGB2Lum (i3710, the first, input FILT 1, 4 taps) then 16Lum (i3708, input FILT 0, 16 taps), BlurScale = (1 / w, 1 / h, 0, 0) of
    ''' the input level, DynamicResolutionParam (1, 1, 1, 1) (0x14220A215 / 0x14220A235), down to 1 x 1.</summary>
    Private Sub RunPyramid(progs As Fo4DeferredPrograms, pyr As List(Of (Tex As Integer, W As Integer, H As Integer)))
        For k = 1 To pyr.Count - 1
            Dim first = k = 1
            Dim p = progs.ImageSpaceProgram(If(first, 3710UI, 3708UI))
            p.Use()
            p.SetVector4("BlurScale", New Vector4(1.0F / pyr(k - 1).W, 1.0F / pyr(k - 1).H, 0.0F, 0.0F))
            p.SetVector4("DynamicResolutionParam", Vector4.One)
            p.SetVector4Array("BlurOffsets", If(first, Offsets4, Offsets16))
            Pass(p, pyr(k).Tex, pyr(k).W, pyr(k).H, {(0, pyr(k - 1).Tex, first)})
        Next
    End Sub

    ''' <summary>Runs the chain over the scene target's radiance and coverage into <paramref name="displayFbo"/> (the display target):
    ''' see the class summary. <paramref name="imgs"/> = the frame's image space (its HNAM / CNAM / TNAM and LUT, PreviewImagingTable);
    ''' the tonemap's t3 (RT 0x1E with [0x142F984D4] = 1, 0x14220A56C..604) is a declared hole (H5): what RT 0x1E's alpha holds when the
    ''' post reads it is not traced - the G-buffer's own makes the shapes whose record writes id 4 (technique bit 23, Pipboy_Screen;
    ''' the ensayo measured hair among them) skip the tonemap, which the game does not show - so t3 is a texel of alpha 0: no pixel
    ''' bypasses, the frame as before ola 4 (_noBypass);
    ''' <paramref name="refractionNormals"/> = the frame's refraction normals target, 0 when the frame drew none (ISRefraction's
    ''' IsActive [0x143E71D99]); <paramref name="refractionTint"/> its Tint (RefractionLaw.ImageSpaceConstants);
    ''' <paramref name="setBackground"/> uploads the background's uniforms to Fragment_Present.</summary>
    Public Sub Run(progs As Fo4DeferredPrograms, unpremultiply As Shader_Base_Class, actorMean As Shader_Base_Class, present As Shader_Base_Class,
                   imgs As Fo4ImageSpace, sceneTex As Integer, coverageTex As Integer, bgShadowTex As Integer,
                   refractionNormals As Integer, refractionTint As Vector4, displayFbo As Integer, setBackground As Action(Of Shader_Base_Class),
                   emptyVao As Integer)
        Dim origen = Gl43.Origin                                     ' the caller's origin, restored after the chain
        Gl43.ClipControl(ClipOrigin.UpperLeft)
        Try
            ' 1. RT 1 and the pyramids' inputs.
            Gl43.FramebufferTexture(_fbo, FramebufferAttachment.ColorAttachment0, _rt1, 0)
            Gl43.FramebufferTexture(_fbo, FramebufferAttachment.ColorAttachment1, _actor, 0)
            Gl43.FramebufferTexture(_fbo, FramebufferAttachment.ColorAttachment2, _weight, 0)
            Gl43.DrawBuffers(_fbo, 3, {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2})
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
            GL.Viewport(0, 0, _w, _h)
            unpremultiply.Use()
            Gl43.BindTextureUnit(0, TextureTarget.Texture2D, sceneTex) : Gl43.BindTextureUnit(1, TextureTarget.Texture2D, coverageTex)
            FullscreenPass.Draw(unpremultiply, emptyVao, 2)
            Gl43.FramebufferTexture(_fbo, FramebufferAttachment.ColorAttachment1, 0, 0)
            Gl43.FramebufferTexture(_fbo, FramebufferAttachment.ColorAttachment2, 0, 0)
            ' 2. HDR: RT 0x46, the actor's pyramids and their ratio.
            DownSample4(progs, _rt1, _rt46)
            DownSample4(progs, _actor, _pyrActor(0).Tex)
            DownSample4(progs, _weight, _pyrWeight(0).Tex)
            RunPyramid(progs, _pyrActor)
            RunPyramid(progs, _pyrWeight)
            actorMean.Use()
            Gl43.FramebufferTexture(_fbo, FramebufferAttachment.ColorAttachment0, _avg, 0)
            Gl43.DrawBuffer(_fbo, DrawBufferMode.ColorAttachment0)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
            GL.Viewport(0, 0, 1, 1)
            Gl43.BindTextureUnit(0, TextureTarget.Texture2D, _pyrActor(_pyrActor.Count - 1).Tex)
            Gl43.BindTextureUnit(1, TextureTarget.Texture2D, _pyrWeight(_pyrWeight.Count - 1).Tex)
            FullscreenPass.Draw(actorMean, emptyVao, 2)
            ' The bloom (effect 43, 0x14220A350..47D; Init 0x142234F10; constants 0x1422351F0): pass 1 BrightPassBlur15 (i3768)
            ' RT 0x46 (FILT 0) -> RT 0x44, BlurBrightPass = (BloomThreshold ISM + 0x88, BloomScale ISM + 0x8C, 0, 0) (0x1422357C6), the
            ' vertical offsets, t1 = the adaptation's result; pass 2 Blur15 (i3737) RT 0x44 -> RT 0x45, the horizontal offsets (both
            ' over RT 0x44's size).
            Dim bw = Math.Max(_w >> 2, 1), bh = Math.Max(_h >> 2, 1)
            Dim bright = progs.ImageSpaceProgram(3768UI)
            bright.Use()
            Dim bloomScale = imgs.BloomScale
#If DEBUG Then
            ' GATE (ShadowGate --fo4-post-scenes, gates-v5): BloomScale 0 - the frame without the bloom, its subject control
            If Fo4GBufferConstants.GateMutantOn("post-bloom-scale-zero") Then bloomScale = 0.0F
#End If
            bright.SetVector4("BlurBrightPass", New Vector4(imgs.BloomThreshold, bloomScale, 0.0F, 0.0F))
            bright.SetVector4Array("BlurOffsets", BloomOffsets(bh, vertical:=True))
            Pass(bright, _rt44, bw, bh, {(0, _rt46, False), (1, _avg, False)})
            Dim blur = progs.ImageSpaceProgram(3737UI)
            blur.Use()
            blur.SetVector4Array("BlurOffsets", BloomOffsets(bw, vertical:=False))
            Pass(blur, _rt45, bw, bh, {(0, _rt44, False)})
            ' The tonemap HDRTonemapBlendCinematic (i3700; ImageSpaceEffectHDR slot 9 0x14220A730): Param, Cinematic, Tint
            ' (Fo4ImageSpace.UploadTonemap), DynamicResolutionParam (1, 1, 1, 1) (0x14220A921); t0 RT 1 (FILT 0), t1 the bloom RT 0x45
            ' (FILT 1, 0x14220A519), t2 the mean (FILT 0), t3 the hole H5 (_noBypass, see Run).
            Dim tone = progs.ImageSpaceProgram(3700UI)
            tone.Use()
            imgs.UploadTonemap(tone)
            tone.SetVector4("DynamicResolutionParam", Vector4.One)
            Pass(tone, _tone, _w, _h, {(0, _rt1, False), (1, _rt45, True), (2, _avg, False), (3, _noBypass, False)})
            ' 3. ISRefraction (effect 5, i3658; its own Render 0x142255B70, RE_REFRACTION_BOTH 4.6): t0 the tonemapped image with
            ' +0x1C = 1 (FILT 1, bilinear), t1 the refraction normals with +0x1C = 0 (POINT).
            Dim graded = _tone
            If refractionNormals <> 0 Then
                Dim refr = progs.ImageSpaceProgram(3658UI)
                refr.Use()
                refr.SetVector4("Tint", refractionTint)
                Pass(refr, _refracted, _w, _h, {(0, _tone, True), (1, refractionNormals, False)})
                graded = _refracted
            End If
            ' 4. GammaCorrectLUT (effect 15, i3648; writer 0x1421AA3A0): t0 the image (FILT 0), t3..t6 the four LUTs (ADDR 0, FILT 1),
            ' LUTWeights: one image space = one LUT with weight 1, the other three the identity with weight 0 (0x1406BFDF0,
            ' 0x1404AFE1A..E22); a LUT that does not load is the identity (0x14218DBFA / DCF8) with its weight.
            Dim lutPass = progs.ImageSpaceProgram(3648UI)
            lutPass.Use()
            lutPass.SetVector4("LUTWeights", New Vector4(1.0F, 0.0F, 0.0F, 0.0F))
#If DEBUG Then
            If Fo4GBufferConstants.GateMutantOn("post-lut-noweight") Then lutPass.SetVector4("LUTWeights", Vector4.Zero)
#End If
            Dim lut = EnsureLut(imgs.LutPath)
            For u = 3 To 6
                Gl43.BindTextureUnit(u, TextureTarget.Texture3D, If(u = 3 AndAlso lut <> 0, lut, IdentityLut()))
                GL.BindSampler(u, _linear)
            Next
            Pass(lutPass, _present, _w, _h, {(0, graded, False)})
            For u = 3 To 6 : Gl43.UnbindTextureUnit(u) : GL.BindSampler(u, 0) : Next
        Finally
            Gl43.ClipControl(origen)
        End Try
        ' 5. The preview's composite over the background, into the display target.
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, displayFbo)
        GL.Viewport(0, 0, _w, _h)
        present.Use()
        setBackground(present)
        Gl43.BindTextureUnit(0, TextureTarget.Texture2D, _present) : Gl43.BindTextureUnit(1, TextureTarget.Texture2D, coverageTex)
        Gl43.BindTextureUnit(2, TextureTarget.Texture2D, bgShadowTex)
        FullscreenPass.Draw(present, emptyVao, 3)
        GL.DepthMask(True)
        GL.Enable(EnableCap.DepthTest)
    End Sub

    ''' <summary>The image space's LUT as the engine samples it (SceneTargets.LutVolume: the loader's 16^3 volume, UNORM, linear,
    ''' clamped); 0 when the engine binds the identity instead. The verdict is kept per path; only NoFile is asked again next frame
    ''' (the data dictionary may not give the file yet).</summary>
    Private Function EnsureLut(path As String) As Integer
        If _lutPath IsNot Nothing AndAlso String.Equals(_lutPath, path, StringComparison.OrdinalIgnoreCase) Then Return _lut
        If _lut <> 0 Then GL.DeleteTexture(_lut) : _lut = 0
        _lutPath = Nothing
        Dim r = SceneTargets.LutVolume(path)
        If r.State = SceneTargets.LutLoad.NoFile Then Return 0
        If r.State = SceneTargets.LutLoad.Loaded Then _lut = Volume(r.Volume)
        _lutPath = path
        Return _lut
    End Function

    ''' <summary>The engine's identity LUT [0x143D872C8] (0x14183F500: 16^3 RGBA8, r, g, b = 17 k, a = 255; the alpha is not sampled
    ''' by i3648, RGB8 here as the file LUTs).</summary>
    Private Function IdentityLut() As Integer
        If _identityLut <> 0 Then Return _identityLut
        Dim vol(16 * 16 * 16 * 3 - 1) As Byte
        For b = 0 To 15
            For g = 0 To 15
                For r = 0 To 15
                    Dim i = ((b * 16 + g) * 16 + r) * 3
                    vol(i) = CByte(17 * r) : vol(i + 1) = CByte(17 * g) : vol(i + 2) = CByte(17 * b)
                Next
            Next
        Next
        _identityLut = Volume(vol)
        Return _identityLut
    End Function

    Private Shared Function Volume(vol As Byte()) As Integer
        Dim t = Gl43.CreateTexture(TextureTarget.Texture3D)
        Gl43.TexStorage3D(t, TextureTarget3d.Texture3D, 1, SizedInternalFormat.Rgb8, 16, 16, 16)
        Dim prevUnpack As Integer
        GL.GetInteger(GetPName.UnpackAlignment, prevUnpack)
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1)
        Gl43.TexSubImage3D(t, TextureTarget.Texture3D, 0, 0, 0, 0, 16, 16, 16, PixelFormat.Rgb, PixelType.UnsignedByte, vol)
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, prevUnpack)
        Return t
    End Function

    ''' <summary>GATE ONLY: the chain's targets of the last frame (ShadowGate --fo4-post-law).</summary>
    Friend ReadOnly Property GateTextures As (Rt1 As Integer, Rt46 As Integer, Avg As Integer, Bloom As Integer, Tone As Integer, Present As Integer)
        Get
            Return (_rt1, _rt46, _avg, _rt45, _tone, _present)
        End Get
    End Property

    Public Sub Free()
        For Each t In {_rt1, _actor, _weight, _tone, _refracted, _present, _rt46, _avg, _rt44, _rt45, _noBypass}
            If t <> 0 Then GL.DeleteTexture(t)
        Next
        For Each pyr In {_pyrActor, _pyrWeight}
            For Each l In pyr : GL.DeleteTexture(l.Tex) : Next
            pyr.Clear()
        Next
        _rt1 = 0 : _actor = 0 : _weight = 0 : _tone = 0 : _refracted = 0 : _present = 0 : _rt46 = 0 : _avg = 0 : _rt44 = 0 : _rt45 = 0 : _noBypass = 0
        If _fbo <> 0 Then GL.DeleteFramebuffer(_fbo) : _fbo = 0
        _w = 0 : _h = 0
    End Sub

    ''' <summary>Frees the frame's targets, the triangle, the samplers and the LUTs (the context must be current).</summary>
    Public Sub FreeAll()
        Free()
        If _vao <> 0 Then GL.DeleteVertexArray(_vao) : _vao = 0
        If _vbo <> 0 Then GL.DeleteBuffer(_vbo) : _vbo = 0
        For Each s In {_point, _linear}
            If s <> 0 Then GL.DeleteSampler(s)
        Next
        _point = 0 : _linear = 0
        If _lut <> 0 Then GL.DeleteTexture(_lut) : _lut = 0
        If _identityLut <> 0 Then GL.DeleteTexture(_identityLut) : _identityLut = 0
        _lutPath = Nothing
    End Sub
End Class

''' <summary>The programs of the chain's app passes (Fo4ImageSpaceAppSource), one context.</summary>
Friend NotInheritable Class Fo4ImageSpaceAppPrograms
    Friend ReadOnly Unpremultiply, ActorMean, Present As Fo4_Deferred_Shader_Class
    Friend Sub New()
        Try
            Unpremultiply = New Fo4_Deferred_Shader_Class(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4ImageSpaceAppSource.Fragment_Unpremultiply)
            ActorMean = New Fo4_Deferred_Shader_Class(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4ImageSpaceAppSource.Fragment_ActorMean)
            Present = New Fo4_Deferred_Shader_Class(BackgroundFadeSource.Vertex_Background, Fo4ImageSpaceAppSource.Fragment_Present)
        Catch
            Dispose()
            Throw
        End Try
    End Sub
    Friend Sub Dispose()
        For Each p In {Unpremultiply, ActorMean, Present}
            p?.Dispose()
        Next
    End Sub
End Class
