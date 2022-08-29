using System.Buffers;
using Limilabs.FTP.Client;
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

    public static void Ftp()
    {
        using var ftp = new Ftp();
        ftp.Connect("localhost");  // or ConnectSSL for SSL/TLS
        ftp.Login("anonymous", "domen@test.com");

        Console.WriteLine(ftp.GetCurrentFolder());
        // ftp.ChangeFolder("drive");
        ftp.Upload("report.txt", "wwwroot/test/testfile.txt");
         
        ftp.Close();
    }
}