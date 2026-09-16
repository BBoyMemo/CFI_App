namespace CfiApp.Application.Media;

public sealed record MediaAssetDto(int Id, string ContentType, long ByteSize, DateTimeOffset UploadedAt);
