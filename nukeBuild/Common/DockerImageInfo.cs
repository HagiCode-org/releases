public record DockerImageInfo(string Registry, string Namespace, string ImageName)
{
    public string FullImageName => string.IsNullOrEmpty(Namespace)
        ? $"{Registry}/{ImageName}"
        : $"{Registry}/{Namespace}/{ImageName}";

    public DockerImageWithTag WithTag(string tag) => new(this, tag);
}

public record DockerImageWithTag(DockerImageInfo ImageInfo, string Tag)
{
    public string FullImageNameWithTag => $"{ImageInfo.FullImageName}:{Tag}";
}
