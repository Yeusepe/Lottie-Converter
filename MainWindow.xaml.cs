using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using LottieConverter.Models;
using LottieConverter.Helpers;
using System.Text.Json;
using System.IO;

namespace LottieConverter
{
    public sealed partial class MainWindow : Window
    {
        private List<StorageFile> _inputFiles = new();
        private bool _isProcessing = false;

        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendsContentIntoTitleBar = true;
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            if (_isProcessing)
            {
                e.AcceptedOperation = DataPackageOperation.None;
                return;
            }

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                DropOverlay.Opacity = 0.8;
            }
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropOverlay.Opacity = 0;

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    _inputFiles.Clear();
                    foreach (var item in items)
                    {
                        if (item is StorageFile file)
                        {
                            var type = file.ContentType.ToLower();
                            if (type.StartsWith("image/") || type.StartsWith("video/"))
                            {
                                _inputFiles.Add(file);
                            }
                        }
                        else if (item is StorageFolder folder)
                        {
                            // If folder, get all images
                            var files = await folder.GetFilesAsync();
                            foreach(var f in files)
                            {
                                if (f.ContentType.ToLower().StartsWith("image/"))
                                {
                                    _inputFiles.Add(f);
                                }
                            }
                        }
                    }

                    if (_inputFiles.Count > 0)
                    {
                        StatusText.Text = $"{_inputFiles.Count} files ready to convert.";
                        ConvertButton.IsEnabled = true;
                    }
                    else
                    {
                        StatusText.Text = "No valid image or video files found.";
                        ConvertButton.IsEnabled = false;
                    }
                }
            }
        }

        private async void ConvertButton_Click(object sender, RoutedEventArgs e)
        {
            if (_inputFiles.Count == 0 || _isProcessing) return;

            _isProcessing = true;
            ConvertButton.IsEnabled = false;
            ConversionProgressBar.Visibility = Visibility.Visible;
            ConversionProgressBar.Value = 0;
            StatusText.Text = "Starting conversion...";

            try
            {
                // Config
                if (!double.TryParse(FpsInput.Text, out double fps)) fps = 30;
                
                var selectedRes = (ResolutionCombo.SelectedItem as ComboBoxItem)?.Tag.ToString();
                var parts = selectedRes.Split(',');
                int targetW = int.Parse(parts[0]);
                int targetH = int.Parse(parts[1]);

                // Determine if video or image sequence
                // For now, if we have a single MP4, we treat it as video source to extract frames
                // If we have multiple images, we treat them as sequence.
                
                List<StorageFile> frames = new();
                bool tempFrames = false;
                StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;

                // Handle single video file scenario
                if (_inputFiles.Count == 1 && _inputFiles[0].ContentType.StartsWith("video/"))
                {
                    StatusText.Text = "Extracting frames from video...";
                    frames = await MediaHelper.GetFramesFromVideoAsync(_inputFiles[0], tempFolder);
                    tempFrames = true;
                }
                else
                {
                    // Sort input files by name to ensure sequence order
                    frames = _inputFiles.OrderBy(f => f.Name).ToList();
                }

                if (frames.Count == 0)
                {
                    throw new Exception("No frames found to process.");
                }

                StatusText.Text = $"Processing {frames.Count} frames...";

                // Create Lottie Structure
                // We need dimensions of the first frame if targetW/H is 0
                int finalW = targetW;
                int finalH = targetH;
                
                if (finalW == 0 || finalH == 0)
                {
                    using (var stream = await frames[0].OpenReadAsync())
                    {
                        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                        finalW = (int)decoder.PixelWidth;
                        finalH = (int)decoder.PixelHeight;
                    }
                }

                var root = new LottieRoot
                {
                    FrameRate = fps,
                    Width = finalW,
                    Height = finalH,
                    Name = "Converted Animation",
                    OutPoint = frames.Count
                };

                for (int i = 0; i < frames.Count; i++)
                {
                    var percent = ((double)i / frames.Count) * 100;
                    ConversionProgressBar.Value = percent;

                    var file = frames[i];
                    var b64 = await MediaHelper.ConvertToBase64Async(file, finalW, finalH);
                    var assetId = $"img_{i}";

                    root.Assets.Add(new LottieAsset
                    {
                        Id = assetId,
                        Width = finalW,
                        Height = finalH,
                        ImageData = $"data:image/png;base64,{b64}"
                    });

                    root.Layers.Add(new LottieLayer
                    {
                        Index = i + 1,
                        Name = $"Frame_{i}",
                        RefId = assetId,
                        InPoint = i,
                        OutPoint = i + 1,
                        Transform = new LottieTransform
                        {
                            Opacity = new LottieProperty { Value = 100 },
                            Rotation = new LottieProperty { Value = 0 },
                            Position = new LottieMultiProperty { Value = new List<double> { finalW / 2.0, finalH / 2.0, 0 } },
                            AnchorPoint = new LottieMultiProperty { Value = new List<double> { finalW / 2.0, finalH / 2.0, 0 } },
                            Scale = new LottieMultiProperty { Value = new List<double> { 100, 100, 100 } }
                        }
                    });
                }

                // Save
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                savePicker.FileTypeChoices.Add("JSON File", new List<string>() { ".json" });
                savePicker.SuggestedFileName = "animation.json";
                
                // WinUI 3 Window Handle
                var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

                var outFile = await savePicker.PickSaveFileAsync();
                if (outFile != null)
                {
                    var options = new JsonSerializerOptions { WriteIndented = false, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
                    // Python script used minimal separators, but standard default is fine for modern players.
                    // To match python's smaller size we can minimize.
                    
                    var json = JsonSerializer.Serialize(root, options);
                    await FileIO.WriteTextAsync(outFile, json);
                    StatusText.Text = $"Saved to {outFile.Name}";
                }
                else
                {
                    StatusText.Text = "Save cancelled.";
                }
                
                // Cleanup temp frames if video
                if (tempFrames)
                {
                    // Should delete the temp files/folder? 
                    // For now, let's just leave them or try to delete.
                    foreach(var f in frames)
                    {
                        try { await f.DeleteAsync(); } catch { }
                    }
                }

            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = ex.Message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                _isProcessing = false;
                ConvertButton.IsEnabled = true;
                ConversionProgressBar.Visibility = Visibility.Collapsed;
            }
        }
        
    }
}
