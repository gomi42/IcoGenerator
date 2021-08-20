using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;

namespace IcoGenerator
{
    public class FileInfo
    {
        public string FullFilename { get; set; }
        public string Filename { get; set; }
        public string SizeDisplay { get; set; }
        public System.Drawing.Size Size { get; set; }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private List<FileInfo> loadedFiles = new List<FileInfo>();

        public MainWindow()
        {
            InitializeComponent();
            AllowDrop = true;
        }

        /// <summary>
        /// Test whether drag and drop is allowed
        /// </summary>
        protected override void OnDragOver(DragEventArgs e)
        {
            try
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                e.Effects = DragDropEffects.Move;

                foreach (var file in files)
                {
                    var ext = Path.GetExtension(file);

                    if (string.Compare(ext, ".png", true) != 0)
                    {
                        e.Effects = DragDropEffects.None;
                        break;
                    }
                }
            }
            catch
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        /// <summary>
        /// Accept dropping a file
        /// </summary>
        protected override void OnDrop(DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            AddFiles(files);
        }

        private void OnAddButtonClicked(object sender, RoutedEventArgs e)
        {
            var openDialog = new System.Windows.Forms.OpenFileDialog();
            openDialog.Filter = "Image File (*.png)|*.png";
            openDialog.Multiselect = true;
            var result = openDialog.ShowDialog();

            if (result == System.Windows.Forms.DialogResult.OK)
            {
                AddFiles(openDialog.FileNames);
            }
        }

        private void AddFiles(string[] filenames)
        {
            foreach (var file in filenames)
            {
                if (loadedFiles.Exists(x => x.FullFilename == file))
                {
                    continue;
                }

                var size = GetImageSize(file);

                if (size.Height > 256 || size.Width > 256)
                {
                    continue;
                }

                var loaded = loadedFiles.Find(x => x.Size == size);

                if (loaded != null)
                {
                    loadedFiles.Remove(loaded);
                }

                var filename = Path.GetFileName(file);
                loadedFiles.Add(new FileInfo { FullFilename = file, Filename = filename, Size = size, SizeDisplay = $"{size.Width}x{ size.Height }" });
            }

            loadedFiles.Sort((x, y) =>
            {
               if (x.Size.Width == y.Size.Width)
                {
                    return 0;
                }

                return x.Size.Width < y.Size.Width ? -1 : 1;
            });
            FileList.ItemsSource = null;
            FileList.ItemsSource = loadedFiles;
            Hint.Visibility = Visibility.Collapsed;
        }

        private void OnClearButtonClicked(object sender, RoutedEventArgs e)
        {
            loadedFiles.Clear();
            FileList.ItemsSource = null;
            Hint.Visibility = Visibility.Visible;
        }

        private void OnExportButtonClicked(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "ICO File (*.ico)|*.ico";
            var result = saveDialog.ShowDialog();

            if (result == true)
            {
                ExportIco(saveDialog.FileName);
            }
        }

        private void ExportIco(string icoFilename)
        {
            try
            {
                FileStream outputFile = new FileStream(icoFilename, FileMode.Create);
                BinaryWriter binWriter = new BinaryWriter(outputFile);

                List<long> offsets = new List<long>();
                // icon dir

                // must be 0
                binWriter.Write((short)0);

                // type
                binWriter.Write((short)1);

                // # of images
                binWriter.Write((short)loadedFiles.Count);

                // ICONDIRENTRY 

                foreach (var file in loadedFiles)
                {
                    // width
                    var width = file.Size.Width;
                    binWriter.Write((byte)(width == 256 ? 0 : width));

                    // height
                    var height = file.Size.Height;
                    binWriter.Write((byte)(height == 256 ? 0 : height));

                    // # colors in palette
                    binWriter.Write((byte)0);

                    // reserved => 0
                    binWriter.Write((byte)0);

                    // # color planes
                    binWriter.Write((short)1);

                    // # bit per pixel
                    binWriter.Write((short)0);

                    // remember file position - will be patched later
                    offsets.Add(outputFile.Position);

                    // size of the image
                    binWriter.Write((uint)0);

                    // offset of the image in file
                    binWriter.Write((uint)0);
                }

                int resIndex = 0;

                foreach (var file in loadedFiles)
                {
                    var startPosition = outputFile.Position;

                    using (var fileStream = new FileStream(file.FullFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var inputBitmap = Image.FromStream(fileStream);
                        Bitmap newBitmap = new Bitmap(inputBitmap);

                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            newBitmap.Save(memoryStream, ImageFormat.Png);
                            binWriter.Write(memoryStream.ToArray());
                        }
                    }

                    var currentPosition = outputFile.Position;

                    var size = currentPosition - startPosition;
                    outputFile.Position = offsets[resIndex];

                    // patch size
                    binWriter.Write((uint)size);

                    // patch offset of the image
                    binWriter.Write((uint)startPosition);

                    // set position back to the end
                    outputFile.Position = currentPosition;
                    resIndex++;
                }

                outputFile.Close();
                outputFile.Dispose();
            }
            catch
            {
            }
        }

        private System.Drawing.Size GetImageSize(string imageFilename)
        {
            System.Drawing.Size size;

            using (var fileStream = new FileStream(imageFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var image = Image.FromStream(fileStream, false, false))
                {
                    size = new System.Drawing.Size(image.Width, image.Height);
                }
            }

            return size;
        }
    }
}
