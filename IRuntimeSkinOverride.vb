Imports NiflySharp.Blocks

''' <summary>
''' Capability interface implemented by shapes that support runtime synthetic skin injection.
'''
''' Use case: shapes that are NOT skinned in their NIF (no NiSkinInstance block, no per-vertex
''' bone data) but need to render anchored to a bone at runtime. Example: LightPlane in a
''' Protectron HeadProtectron.nif that mounts via BSConnectPoint — without synthetic skin it
''' renders at world origin instead of attached to the chunk anchor.
'''
''' Contract:
'''   - When ApplySyntheticAnchorSkin has been called, the shape behaves as skinned to the
'''     supplied anchor bone with weight 1.0 on every vertex. The bind transform is the
'''     shape's chunk-local NIFtree position (i.e. what GetGlobalTransform(backing, nif)
'''     returns for the unmodified shape).
'''   - IsSkinned, ShapeBones, ShapeBoneTransforms (on IRenderableShape) reflect the
'''     synthetic state once set.
'''   - HasSyntheticSkin is the discriminator for V2 reskin and other paths that need to
'''     treat synthetic-skinned shapes specially (typically: skip their custom processing).
'''
''' This interface is OPT-IN. Implementers that don't support runtime override don't need to
''' implement it. Callers must TryCast(shape, IRuntimeSkinOverride) before invoking.
''' </summary>
Public Interface IRuntimeSkinOverride

    ''' <summary>True once ApplySyntheticAnchorSkin has been called on this shape.</summary>
    ReadOnly Property HasSyntheticSkin As Boolean

    ''' <summary>
    ''' Inject runtime synthetic skin tying the entire shape to a single anchor bone.
    '''
    ''' anchorBone: in-memory NiNode whose .Name.String matches a bone in the actor's
    '''             SkeletonInstance.SkeletonDictionary. Does NOT need to exist in the NIF
    '''             blocks list — only the name is used for dictionary lookup at skin time.
    '''
    ''' bindTransform: shape-to-bone bind matrix. Typically the chunk-local NIFtree global
    '''                transform of the shape's backing NiNode (what
    '''                Transform_Class.GetGlobalTransform(backing, shape.NifContent) returns).
    '''                When composed with the anchor's posed world it produces:
    '''                  vertex_world = vertex_local × bindTransform × anchor.posedWorld
    ''' </summary>
    Sub ApplySyntheticAnchorSkin(anchorBone As NiNode, bindTransform As Transform_Class)

End Interface
