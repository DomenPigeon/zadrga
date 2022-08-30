using System.Buffers;
using Limilabs.FTP.Client;
using Microsoft.AspNetCore.Components;

namespace CloudServer.Logic;

public static class FileManager {
    public const int KiloByte = 1024;
    public const int MegaByte = 1024 * 1024;
    public const long GigaByte = 1024 * 1024 * 1024;

    public static class Settings {
        public static int MaxAllowedMemoryChunkSizePerFileBytes = 100 * KiloByte;
        public static long MaxFileSizeBytes = 100 * GigaByte;
        public static int MaxAllowedFiles = 100;
    }

    public static MarkupString LogDisksSizes() {
        var info = DriveInfo.GetDrives().Select(drive => $"Drive: {drive.Name}, Total: {drive.TotalSize / GigaByte} GB, Free: {drive.TotalFreeSpace / GigaByte} GB");
        return new MarkupString(string.Join("<br>", info));
    }

    public static class SignalR {
        public static async Task UploadFileAsync(File file, string remoteDirectory) {
            try {
                await UploadFileInternalAsync(file, remoteDirectory);
            }
            catch (Exception ex) {
                file.Error = ex;
                file.Status = FileUploadStatus.Error;
            }
        }

        // TODO: It would be a good idea to put this whole code in try finally block to make sure that memory is always returned to the pool.
        private static async Task UploadFileInternalAsync(File file, string remoteDirectory) {
            file.Status = FileUploadStatus.Uploading;
            var path = Path.Combine(remoteDirectory, file.BrowserFile.Name);

            await using var stream = file.BrowserFile.OpenReadStream(Settings.MaxFileSizeBytes);
            await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

            var defaultMemorySize = ArrayPool<byte>.Shared.Rent(Settings.MaxAllowedMemoryChunkSizePerFileBytes);
            var processedBytes = 0L;
            while (processedBytes < file.BrowserFile.Size) {
                var memorySize = (int) Math.Min(file.BrowserFile.Size - processedBytes, Settings.MaxAllowedMemoryChunkSizePerFileBytes);
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

                file.ProgressPercentage = (float) processedBytes / file.BrowserFile.Size * 100;
            }

            ArrayPool<byte>.Shared.Return(defaultMemorySize);

            file.FinishLoading();
        }
    }

    public static class FileTransferProtocol {
        public static void UploadFileAsync(File file, string directory) {
            using var client = new Ftp();
            client.Connect("localhost"); // or ConnectSSL for SSL/TLS
            client.Login("anonymous", "domen@test.com");

            var path = Path.Combine(directory, file.BrowserFile.Name);
            using var stream = file.BrowserFile.OpenReadStream(Settings.MaxFileSizeBytes);
            file.Status = FileUploadStatus.Uploading;
            client.Upload(path, stream);
            client.Close();
            
            file.FinishLoading();
        }
    }
}
