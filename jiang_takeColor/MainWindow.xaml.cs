using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Interop;
using jiang_takeColor.Class;
using System.IO;
using System.Windows.Media;
using System.Drawing.Imaging;
using System.Windows.Threading;

namespace jiang_takeColor
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        MouseXY.POINT Mouse;
        BrushConverter brushConverter = new BrushConverter();

        public MainWindow() {
            InitializeComponent();
            
            this.Loaded += (sender, e) => {
                var wpfHwnd = new WindowInteropHelper(this).Handle;
                var hWndSource = HwndSource.FromHwnd(wpfHwnd);
                if (hWndSource != null) hWndSource.AddHook(MainWindowProc);
                Win32.RegisterHotKey(wpfHwnd, Win32.GlobalAddAtom("Alt-C"), Win32.KeyModifiers.Alt, (int)Keys.C);
                Mouse = new MouseXY.POINT();
                StartReadScreen();
                Console.WriteLine("-------------加载完成---------------");
            };
        }

        // 截取屏幕从截取的屏幕中读取颜色
        public void GetScreenSnapshot(object sender = null, EventArgs e = null) {
            try {
                DateTime now = DateTime.Now;
                var bitmap = new Bitmap(10, 10, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                using (Graphics memoryGrahics = Graphics.FromImage(bitmap))
                {
                    MouseXY.GetCursorPos(out Mouse);
                    memoryGrahics.CopyFromScreen(Mouse.X - 5 , Mouse.Y - 5, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
                }
                MemoryStream ms = new MemoryStream();
                bitmap.Save(ms, ImageFormat.Bmp);
                byte[] bytes = ms.GetBuffer();  //byte[]   bytes=   ms.ToArray(); 这两句都可以
                ms.Close();
                //Convert it to BitmapImage
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = new MemoryStream(bytes);
                image.EndInit();
                MyImage.Source = image;
            } catch (Exception) {
                Console.WriteLine("-----------------截图错误----------------");
            }
        }

        // 事件相应函数
        private IntPtr MainWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            switch (msg) {
                case Win32.WmHotkey:
                    int sid = wParam.ToInt32();
                    if (sid == Win32.GlobalAddAtom("Alt-C"))
                    {
                        MouseXY.GetCursorPos(out Mouse);
                        IntPtr hdc = Win32.GetDC(new IntPtr(0));
                        System.Drawing.Point p = new System.Drawing.Point(Mouse.X, Mouse.Y);
                        // 一个颜色通道为8为二进制，通过二进制并且运算和位移截取颜色并进行处理
                        int c = Win32.GetPixel(hdc, p), r = c & 0xFF, g = (c & 0xFF00) >> 8, b = (c & 0xFF0000) >> 16;
                        string clrstr = "#" + ParseColorToString(r) + ParseColorToString(g) + ParseColorToString(b);
                        //Console.WriteLine(Convert.ToString(c, 16));
                        //Console.WriteLine(Convert.ToString(c & 0xFFffffff, 16));
                        //Console.WriteLine(clrstr);
                        ListBoxItem item = NewColorItem(clrstr);
                        ColorList.Items.Add(item);
                        ColorText.Text = clrstr;
                        ColorList.ScrollIntoView(item);
                        System.Windows.Forms.Clipboard.SetDataObject(clrstr);
                    }
                    handled = true;
                break;
            }

            return IntPtr.Zero;
        }
        
        private ListBoxItem NewColorItem (string str) {
            Grid grid = new Grid() { Background = (System.Windows.Media.Brush)brushConverter.ConvertFromString("White"), Width = 70 };
            grid.Children.Add(new TextBlock() { Text = str });
            ListBoxItem ListItem = new ListBoxItem() { 
                Content = grid,
                Tag = str,
                ToolTip = "双击复制色值",
                Background  = (System.Windows.Media.Brush)brushConverter.ConvertFromString(str)
            };
            ListItem.MouseDoubleClick += ListBoxItem_MouseDoubleClick;
            return ListItem;
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            System.Windows.Forms.Clipboard.SetDataObject(((ListBoxItem)sender).Tag);
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler((object s, EventArgs ee) =>
            {
                ((ListBoxItem)sender).IsSelected = false;
            });
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            dispatcherTimer.Start();
        }

        public Bitmap Magnifier(Bitmap srcbitmap, int multiple) {
            if (multiple <= 0) { multiple = 0; return srcbitmap; }
            Bitmap bitmap = new Bitmap(srcbitmap.Size.Width * multiple, srcbitmap.Size.Height * multiple);
            BitmapData srcbitmapdata = srcbitmap.LockBits(new Rectangle(new System.Drawing.Point(0, 0), srcbitmap.Size), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            BitmapData bitmapdata = bitmap.LockBits(new Rectangle(new System.Drawing.Point(0, 0), bitmap.Size), ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            unsafe
            {
                byte* srcbyte = (byte*)(srcbitmapdata.Scan0.ToPointer());
                byte* sourcebyte = (byte*)(bitmapdata.Scan0.ToPointer());
                for (int y = 0; y < bitmapdata.Height; y++)
                {
                    for (int x = 0; x < bitmapdata.Width; x++)
                    {
                        long index = (x / multiple) * 4 + (y / multiple) * srcbitmapdata.Stride;
                        sourcebyte[0] = srcbyte[index];
                        sourcebyte[1] = srcbyte[index + 1];
                        sourcebyte[2] = srcbyte[index + 2];
                        sourcebyte[3] = srcbyte[index + 3];
                        sourcebyte += 4;
                    }
                }
            }
            srcbitmap.UnlockBits(srcbitmapdata);
            bitmap.UnlockBits(bitmapdata);
            return bitmap;
        }

        public void StartReadScreen() {
            DispatcherTimer dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(GetScreenSnapshot);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            dispatcherTimer.Start();
        }

        public string ParseColorToString (int n) {
            if (n == 0)
            {
                return "00";
            } else if (n < 16) {
                return "0" + Convert.ToString(n, 16);
            } else
            {
                return Convert.ToString(n, 16);
            }
        }
    }
}
