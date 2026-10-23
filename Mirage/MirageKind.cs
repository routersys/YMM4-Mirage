using System.ComponentModel.DataAnnotations;

namespace Mirage;

public enum MirageKind
{
    [Display(Name = nameof(Texts.KindInferior), Description = nameof(Texts.KindInferiorDescription), ResourceType = typeof(Texts))]
    Inferior = 1,

    [Display(Name = nameof(Texts.KindSuperior), Description = nameof(Texts.KindSuperiorDescription), ResourceType = typeof(Texts))]
    Superior = 2,
}

public enum MirageSource
{
    [Display(Name = nameof(Texts.SourcePhoto), Description = nameof(Texts.SourcePhotoDescription), ResourceType = typeof(Texts))]
    Photo = 1,

    [Display(Name = nameof(Texts.SourceScene), Description = nameof(Texts.SourceSceneDescription), ResourceType = typeof(Texts))]
    Scene = 2,
}
