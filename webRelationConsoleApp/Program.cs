using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        //try
        //{
            FileServer();
        //}
        //catch (Exception ex)
        //{
        //    Console.WriteLine("FileServer Error: " + ex.Message);
        //}
    }

    static void baseUrl(string root, HttpListenerRequest req, HttpListenerResponse res)
    {
        // 実際のローカルファイルパス
        string path = root + "/index.html";
        res.ContentType = "text/html";
        // ファイル内容を出力
        try
        {
            res.StatusCode = 200;
            byte[] content = File.ReadAllBytes(path);
            res.OutputStream.Write(content, 0, content.Length);
        }
        catch (Exception ex)
        {
            res.StatusCode = 500; // 404 でも良いのだがここは雑に 500 にまとめておく
            byte[] content = Encoding.UTF8.GetBytes(ex.Message);
            res.OutputStream.Write(content, 0, content.Length);
        }
        res.Close();
    }

    static void baseUrl_image(string root, HttpListenerRequest req, HttpListenerResponse res)
    {
        // 実際のローカルファイルパス
        string path = root + req.RawUrl; //req.RawUrl が　/usbCameraImage.JPEGのようなとき
        res.ContentType = "image/JPEG";
        // ファイル内容を出力
        try
        {
            res.StatusCode = 200;
            byte[] content = File.ReadAllBytes(path);
            res.OutputStream.Write(content, 0, content.Length);
        }
        catch (Exception ex)
        {
            res.StatusCode = 500; // 404 でも良いのだがここは雑に 500 にまとめておく
            byte[] content = Encoding.UTF8.GetBytes(ex.Message);
            res.OutputStream.Write(content, 0, content.Length);
        }

        res.Close();

    }

    static void FileServer()
    {
        CamerasControl camerasControl = new CamerasControl();

        // ドキュメントルート (プロジェクトディレクトリ内 root)
        // 例: "C:\Projects\CsharpHttpServerSample\root"
        string temp = Environment.CurrentDirectory;           //solutionName/bin/debug
        temp = Directory.GetParent(temp).FullName;            //solutionName/bin
        temp = Directory.GetParent(temp).FullName + "/root";  //solutionName  + /root
        string root = temp; //solutionName/root

        HttpListener listener = new HttpListener();
        listener.Prefixes.Add("http://192.168.1.7:8000/");
        listener.Start();
        
        //USBカメラ接続
        Console.WriteLine("\nUsbCameraConnect");
        camerasControl.UsbCameraConnect();
        
        //THETA 基本情報取得
        Console.WriteLine("\nTHETA ConnectTest");
        camerasControl.ThetaConnectTest();
        
        //THETA スリープなし設定
        Console.WriteLine("\nTHETA SetNoSleep");
        camerasControl.ThetaSetNoSleep();
        
        while (true)
        {

            //THETA 撮影前の状態取得
            Console.WriteLine("\nTHETA GetState");
            camerasControl.ThetaGetState();

            //アクセスがあるまでlistener.GetContext() でとまる
            Console.WriteLine("Access waiting : ");
            HttpListenerContext context = listener.GetContext();
            HttpListenerRequest req = context.Request;
            HttpListenerResponse res = context.Response;
            //res.ContentType イメージ: image/拡張子 html: text/html　にする

            // URL (ここには "/" とか "/index.html" 等が入ってくる)
            string urlPath = req.RawUrl;
            Console.WriteLine("Access in : " + urlPath);

            if (req.RawUrl == "/")
            {
                //USBカメラとTHETAの同時撮影関数
                var usbCameraTask = Task.Run(() => {
                    //USBカメラ撮影,focusなし->引数をfalseに(非同期)
                    Console.WriteLine("\nUsbCamera TakeAndSavePicture");
                    camerasControl.UsbCameraCapture(false);
                });
                var thetaTask = Task.Run(() => {
                    //THETA 静止画撮影、保存(非同期)
                    Console.WriteLine("\nTHETA ThetaTakeAndSavePicture");
                    camerasControl.ThetaTakePicture();
                    camerasControl.ThetaSavePicture();
                });
                Task.WaitAll(usbCameraTask, thetaTask);//全てのTaskが完了した時に完了扱いになるまで待つ
                
                baseUrl(root, req, res);
            }
            else if(req.RawUrl == "/usbCameraImage.JPEG" || req.RawUrl == "/thetaImage.JPEG")
            {
                Console.WriteLine("this url is for image");
                baseUrl_image(root, req, res);
            }
        }
    }
}