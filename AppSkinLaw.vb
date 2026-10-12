''' <summary>THE APP'S SKINNED-NORMAL LAW in GLSL - ONE text for the three vertex stages that skin (the transcribed engine VS,
''' Vertex_FO4, Vertex_SSE; user decisions D2 and revisor-22, 12-oct-2026). It is the line-by-line transcription of the VB law
''' FastSkin.UnVertice / EsDegenerada / Rotar (FastSkin.vb:139-188, 256-293, 519-538): cofactors / determinant, identity when
''' degenerate (three guards, EpsDetRel), zero-length vector kept. ASCII, no double quotes (a VB Const String).</summary>
Friend Module AppSkinLaw
    Friend Const Glsl As String = "
// APP SKIN LAW (FastSkin.UnVertice): N' = normalize(cofactor(M) / det * N), identity when degenerate. M given by ROWS r0, r1, r2
// (M * v = (dot(r0,v), dot(r1,v), dot(r2,v))); OpenTK m = transpose(M): mIJ = r(J-1)[I-1].
mat3 appSkinNormalMatrix(vec3 r0, vec3 r1, vec3 r2)
{
    float m11 = r0.x, m12 = r1.x, m13 = r2.x, m21 = r0.y, m22 = r1.y, m23 = r2.y, m31 = r0.z, m32 = r1.z, m33 = r2.z;
    float c11 = m22 * m33 - m23 * m32, c12 = m23 * m31 - m21 * m33, c13 = m21 * m32 - m22 * m31;
    float det = m11 * c11 + m12 * c12 + m13 * c13;
    float c21 = m13 * m32 - m12 * m33, c22 = m11 * m33 - m13 * m31, c23 = m12 * m31 - m11 * m32;
    float c31 = m12 * m23 - m13 * m22, c32 = m13 * m21 - m11 * m23, c33 = m11 * m22 - m12 * m21;
    float r = 1.0 / det;
    float f2 = m11 * m11 + m12 * m12 + m13 * m13 + m21 * m21 + m22 * m22 + m23 * m23 + m31 * m31 + m32 * m32 + m33 * m33;
    float t = f2 * (1.0 / 3.0);
    float q = det / t;
    bool degenerate = !(t > 0.0) || !(abs(det) <= uintBitsToFloat(0x7F7FFFFFu)) || !(abs(1.0 / det) <= uintBitsToFloat(0x7F7FFFFFu))
                      || (q * q <= (0.00001 * 0.00001) * t);
    if (degenerate) return mat3(1.0);
    // rows of E = c * r; GLSL column-major mat3 N with N * v = v * E (FastSkin.Rotar is v * E)
    return mat3(c11 * r, c12 * r, c13 * r,  c21 * r, c22 * r, c23 * r,  c31 * r, c32 * r, c33 * r);
}
vec3 appSkinRotate(mat3 e, vec3 v)
{
    vec3 w = e * v;
    float len2 = w.x * w.x + w.y * w.y + w.z * w.z;
    if (len2 == 0.0) return w;                  // FastSkin.Rotar: zero length kept
    float inv = 1.0 / sqrt(len2);
    return vec3(w.x * inv, w.y * inv, w.z * inv);
}
vec3 appSkinNormal(vec3 r0, vec3 r1, vec3 r2, vec3 n) { return appSkinRotate(appSkinNormalMatrix(r0, r1, r2), n); }
"
End Module
