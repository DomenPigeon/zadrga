namespace CloudServer.Logic;

public enum FileUploadStatus {
    None,
    Uploading,
    Uploaded,

    // TODO: Implement cancellation
    Canceled,

    Error,
}