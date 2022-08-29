using System.Buffers;
using Microsoft.AspNetCore.Components;

namespace CloudServer.Logic;

public static class FileUploader {
    private const int KiloByte = 1024;
    private const int MegaByte = 1024 * 1024;
    private const long GigaByte = 1024 * 1024 * 1024;

    public static class Settings {
        public static int MaxAllowedMemoryChunkSizePerFileBytes = 100 * KiloByte;
        public static long MaxFileSizeBytes = 100 * GigaByte;
        public static int MaxAllowedFiles = 100;
    }

    public static MarkupString DiskSize() {
        var info = DriveInfo.GetDrives().Select(drive => $"Drive: {drive.Name}, Total: {drive.TotalSize / GigaByte} GB, Free: {drive.TotalFreeSpace / GigaByte} GB");
        return new MarkupString(string.Join("<br>", info));
    }

    public static async Task UploadFileAsync(LoadFile loadFile, string directory) {
        try {
            await UploadFileInternalAsync(loadFile, directory);
        }
        catch (Exception ex) {
            loadFile.Error = ex;
            loadFile.Status = FileUploadStatus.Error;
        }
    }

    // TODO: It would be a good idea to put this whole code in try finally block to make sure that memory is always returned to the pool.
    private static async Task UploadFileInternalAsync(LoadFile loadFile, string directory) {
        loadFile.Status = FileUploadStatus.Uploading;
        var path = Path.Combine(directory, loadFile.BrowserFile.Name);

        await using var stream = loadFile.BrowserFile.OpenReadStream(Settings.MaxFileSizeBytes);
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        var defaultMemorySize = ArrayPool<byte>.Shared.Rent(Settings.MaxAllowedMemoryChunkSizePerFileBytes);
        var processedBytes = 0L;
        while (processedBytes < loadFile.BrowserFile.Size) {
            var memorySize = (int) Math.Min(loadFile.BrowserFile.Size - processedBytes, Settings.MaxAllowedMemoryChunkSizePerFileBytes);
            var memory = memorySize == Settings.MaxAllowedMemoryChunkSizePerFileBytes ? defaultMemorySize : ArrayPool<byte>.Shared.Rent(memorySize);

            var readBytes = await stream.ReadAsync(memory, 0, memorySize);
            while (readBytes < memorySize) {
                readBytes += await stream.ReadAsync(memory, readBytes, memorySize - readBytes);
            }

            await fileStream.WriteAsync(memory, 0, readBytes);
            fileStream.Flush();
            processedBytes += readBytes;

            if (memory != defaultMemorySize) {
                ArrayPool<byte>.Shared.Return(memory);
            }

            loadFile.ProgressPercentage = (float) processedBytes / loadFile.BrowserFile.Size * 100;
        }

        ArrayPool<byte>.Shared.Return(defaultMemorySize);

        loadFile.FinishLoading();
    }
}

// FTP file upload example
// https://docs.microsoft.com/en-us/dotnet/framework/network-programming/how-to-upload-files-with-ftp?redirectedfrom=MSDN
// using System;
// using System.IO;
// using System.Net;
// using System.Threading.Tasks;

// namespace Examples.System.Net
// {
//     public class WebRequestGetExample
//     {
//         public static async Task Main()
//         {
//             // Get the object used to communicate with the server.
//             FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://www.contoso.com/test.htm");
//             request.Method = WebRequestMethods.Ftp.UploadFile;

//             // This example assumes the FTP site uses anonymous logon.
//             request.Credentials = new NetworkCredential("anonymous", "janeDoe@contoso.com");

//             // Copy the contents of the file to the request stream.
//             using (FileStream fileStream = File.Open("testfile.txt", FileMode.Open, FileAccess.Read))
//             {
//                 using (Stream requestStream = request.GetRequestStream())
//                 {
//                     await fileStream.CopyToAsync(requestStream);
//                     using (FtpWebResponse response = (FtpWebResponse)request.GetResponse())
//                     {
//                         Console.WriteLine($"Upload File Complete, status {response.StatusDescription}");
//                     }
//                 }
//            }
//         }
//     }
// }


// FTP file download example
// https://docs.microsoft.com/en-us/dotnet/framework/network-programming/how-to-download-files-with-ftp?source=recommendations
// using System;
// using System.IO;
// using System.Net;

// namespace Examples.System.Net
// {
//     public class WebRequestGetExample
//     {
//         public static void Main ()
//         {
//             // Get the object used to communicate with the server.
//             FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp://www.contoso.com/test.htm");
//             request.Method = WebRequestMethods.Ftp.DownloadFile;

//             // This example assumes the FTP site uses anonymous logon.
//             request.Credentials = new NetworkCredential("anonymous","janeDoe@contoso.com");

//             FtpWebResponse response = (FtpWebResponse)request.GetResponse();

//             Stream responseStream = response.GetResponseStream();
//             StreamReader reader = new StreamReader(responseStream);
//             Console.WriteLine(reader.ReadToEnd());

//             Console.WriteLine($"Download Complete, status {response.StatusDescription}");

//             reader.Close();
//             response.Close();
//         }
//     }
// }

// FTP in .NET: https://docs.microsoft.com/en-us/dotnet/framework/network-programming/ftp?source=recommendations