using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LottieConverter.Helpers
{
    public static class MediaHelper
    {
        public static async Task<string> ConvertToBase64Async(StorageFile file, int targetWidth, int targetHeight)
        {
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            return await EncodeStreamToBase64Async(stream, targetWidth, targetHeight);
        }
        
        public static async Task<string> ConvertToBase64Async(ImageStream stream, int targetWidth, int targetHeight)
        {
             return await EncodeStreamToBase64Async(stream, targetWidth, targetHeight);
        }

        private static async Task<string> EncodeStreamToBase64Async(IRandomAccessStream inputStream, int targetWidth, int targetHeight)
        {
            var decoder = await BitmapDecoder.CreateAsync(inputStream);
            
            // Calculate scale
            var transform = new BitmapTransform();
            if (targetWidth > 0 && targetHeight > 0)
            {
                transform.ScaledWidth = (uint)targetWidth;
                transform.ScaledHeight = (uint)targetHeight;
                transform.InterpolationMode = BitmapInterpolationMode.Fant;
            }
            else
            {
                 // Keep original if not specified, but we need width/height for Lottie anyway
                 // so the caller usually provides them.
            }

            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);

            var pixels = pixelData.DetachPixelData();

            using var outStream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outStream);
            
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                transform.ScaledWidth > 0 ? transform.ScaledWidth : decoder.PixelWidth,
                transform.ScaledHeight > 0 ? transform.ScaledHeight : decoder.PixelHeight,
                decoder.DpiX,
                decoder.DpiY,
                pixels);

            await encoder.FlushAsync();

            using var reader = new DataReader(outStream.GetInputStreamAt(0));
            var bytes = new byte[outStream.Size];
            await reader.LoadAsync((uint)outStream.Size);
            reader.ReadBytes(bytes);

            return Convert.ToBase64String(bytes);
        }

        public static async Task<List<StorageFile>> GetFramesFromVideoAsync(StorageFile videoFile, StorageFolder tempFolder)
        {
             // NOTE: MediaComposition is great but extracting every single frame as an image can be slow or tricky depending on codec.
             // A common strategy is to use MediaClip and generate thumbnails or usage of MediaTranscoder?
             // Actually, MediaComposition.GetThumbnailAsync works for single timestamps.
             
             // However, for robust frame-by-frame extraction in simple WinUI without FFmpeg, we can try using MediaComposition.
             
             var clip = await MediaClip.CreateFromFileAsync(videoFile);
             var composition = new MediaComposition();
             composition.Clips.Add(clip);
             
             // We need to know duration and framerate roughly
             // MediaClip doesn't easily give framerate, we might have to assume or ask user.
             // But let's check video properties.
             var props = await videoFile.Properties.GetVideoPropertiesAsync();
             // props.Duration is TimeSpan
             // Frame rate? 
             
             // For now, let's assume 30fps default or try to calculate from properties if available (often 0/empty in UWP).
             // Let's iterate at 30fps for now.
             
             var frameRate = 30; 
             var frameDuration = TimeSpan.FromSeconds(1.0 / frameRate);
             var duration = clip.OriginalDuration;
             
             var frames = new List<StorageFile>();
             
             for (var time = TimeSpan.Zero; time < duration; time += frameDuration)
             {
                 var stream = await composition.GetThumbnailAsync(time, 0, 0, VideoFramePrecision.NearestFrame);
                 if (stream != null)
                 {
                     var file = await tempFolder.CreateFileAsync($"frame_{frames.Count:0000}.png", CreationCollisionOption.ReplaceExisting);
                     using var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                     await RandomAccessStream.CopyAndCloseAsync(stream, fileStream);
                     frames.Add(file);
                 }
             }
             
             return frames;
        }
    }
}
