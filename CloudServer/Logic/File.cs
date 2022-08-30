using Microsoft.AspNetCore.Components.Forms;

namespace CloudServer.Logic;

public class File {
    public readonly IBrowserFile BrowserFile;
    public Exception? Error;
    public FileUploadStatus Status = FileUploadStatus.None;

    public float ProgressPercentage;

    public File(IBrowserFile browserFile) {
        BrowserFile = browserFile;
        ProgressPercentage = 0;
    }

    public void FinishLoading() {
        ProgressPercentage = 100;
        Status = FileUploadStatus.Uploaded;
    }
}