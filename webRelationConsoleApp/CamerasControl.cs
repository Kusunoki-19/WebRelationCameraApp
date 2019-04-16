using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using Ricoh.CameraController;
using System.Threading.Tasks;
using MS.Internal.WindowsBase;

using Newtonsoft.Json;

class CamerasControl
{
    private CameraDevice camera;

    class UserEventListener : CameraEventListener
    {
        public override void ImageAdded(CameraDevice sender, CameraImage image)
        {
            string destinationFolder; //スラッシュを含む行き先フォルダ
            string fileName; //拡張子を含むファイル名
            string filePath; //destinationFolder + fileName

            //image(画像ファイル本体)の取得
            string temp = Environment.CurrentDirectory;//app/bin/debug
            temp = Directory.GetParent(temp).FullName;  //app/bin
            temp = Directory.GetParent(temp).FullName + "/root/";  //app
            destinationFolder =temp;
            fileName = "usbCameraImage.JPEG";
            filePath = destinationFolder + fileName;
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                Response imageGetResponse = image.GetData(fs);
                //ファイルの取得に成功したかどうか
                if (imageGetResponse.Result == Result.OK)
                {
                    Console.WriteLine("USB Camera save Image: " + filePath);
                }
                else
                {
                    foreach (Error error in imageGetResponse.Errors)
                    {
                        Console.WriteLine("Error Code: " + error.Code.ToString() +" / Error Message: " + error.Message);
                    }
                }
            }
            if (image.HasThumbnail == false) return;
            //thumbnailの取得
            fileName = "usbCameraImage_thumb.JPEG";
            filePath = destinationFolder + fileName;
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                //fsで指定したファイルパス(+ファイル名)にファイルをコピーする
                Response imageGetResponse = image.GetThumbnail(fs);
                //ファイルの取得に成功したかどうか
                if (imageGetResponse.Result == Result.OK)
                {
                    Console.WriteLine("USB Camera save Thumb: " + filePath);
                }
                else
                {
                    foreach (Error error in imageGetResponse.Errors)
                    {
                        Console.WriteLine("USB Camera Error Code: " + error.Code.ToString() +" / Error : " + error.Message + ", " + error.Code);
                    }
                }
            }
        }
    }

    class PostReqBody
    {
        public string name { get; set; }            //PostReqBody.name
        public Parameters parameters { get; set; }  //PostReqBody.parameters
        public class Parameters
        {
            public Dictionary<string, int> options {get; set; }     // PostReqBody.parameters.optinos
        }
    }

    class OscStateRes
    {
        // { "fingerprint : "" , "state" : { "storageUri" : urlText , "_cameraError" : [] , ...}}
        public string fingerprint { get; set; }
        public StateRes state { get; set; }
        public class StateRes
        {
            //ストレージURL(string)
            public string storageUri { get; set; }
            //直近の画像ファイルURL(string)
            public string _latestFileUrl { get; set; }
            //カメラエラー(list)
            public IList<string> _cameraError { get; set; }
        }
    }

    string apiPost(string url, PostReqBody reqBody)
    {
        //リクエスト本体 Dict -> text(json形式)
        string bodyText = JsonConvert.SerializeObject(reqBody);

        //リクエスト本体 text(json形式) -> ascii codes(json形式)
        byte[] bodyAscii = Encoding.ASCII.GetBytes(bodyText);

        //reqestオブジェクト作成
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "POST";
        request.ContentType = "application/json;charset=utf-8"; //THETA　APIのoverview参照
        request.Accept = "application/json"; //THETA　APIのoverview参照
        request.ContentLength = bodyAscii.Length;

        //(HttpWebRequest).GetRequestStream : 要求データを書きこむために使用するStreamオブジェクトを取得する
        //そのStreamオブジェクトをrepStreamに代入
        using (Stream reqStream = request.GetRequestStream())
        {
            //(Stream).Write
            //第一引数 : 書き込byteデータ
            //第二引数 : オフセット。byteの書き込み位置。0からはじまる
            //第三引数 : ストリームに書き込むバイト数
            reqStream.Write(bodyAscii, 0, bodyAscii.Length);
        }
        
        try
        {
            WebResponse response = request.GetResponse();
            string resText;
            using (Stream responseStream = response.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    resText = reader.ReadToEnd();
                }
            }
            return resText;
        }
        catch (WebException e)
        {
            Console.WriteLine("API POST Error : " + e.Message);
            return "";
        }
    }

    string apiGet(string url)
    {
        //reqestオブジェクト作成
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.Method = "GET";
        //(HttpWebRequest).GetResponse : インターネットリソースからの応答を返す
        WebResponse response = request.GetResponse();
        string resText;
        using (Stream responseStream = response.GetResponseStream())
        {
            using (StreamReader reader = new StreamReader(responseStream))
            {
                resText = reader.ReadToEnd();
            }
        }
        return resText;
    }

    string thetaAddr = "http://192.168.1.1";
    string stateJson = "";
    PostReqBody reqBody;
    OscStateRes nowState;
    OscStateRes preState;

    public void ThetaConnectTest()
    {
        string thetaAddr = "http://192.168.1.1";
        //API : THETAの基本情報取得 osc/info (GET) 
        string resText = apiGet(thetaAddr + "/osc/info");
        Console.WriteLine(resText);
    }

    public void ThetaSetNoSleep()
    {
        reqBody = new PostReqBody();
        //reqBody.name       = "camera.setOptions"
        //reqBody.parameters = options
        //options = { "sleepDelay", 65535 }
        reqBody.name = "camera.setOptions";
        reqBody.parameters = new PostReqBody.Parameters();
        reqBody.parameters.options = new Dictionary<string, int>() {
            {"sleepDelay", 65535 }
        };
        string resText = apiPost(thetaAddr + "/osc/commands/execute", reqBody);
    }
    public void ThetaGetState()
    {
        //API : 状態の表示 osc/state (POST)
        //reqBody = 空
        reqBody = new PostReqBody(); 
        stateJson = apiPost(thetaAddr + "/osc/state", reqBody);
        //string(json) -> .fingerpritn , .state._latestFileUrl　のようなメンバー型に変換
        preState = JsonConvert.DeserializeObject<OscStateRes>(stateJson);
    }

    public void ThetaTakePicture()
    {
        //API : 写真を撮る osc/commands/execute -> takePicture (POST) 
        //reqBody.name = "camera.takePicture"
        //reqBody.parameters = 空
        reqBody = new PostReqBody();             
        reqBody.name = "camera.takePicture";               
        reqBody.parameters = new PostReqBody.Parameters(); 
        Console.WriteLine("THETA Take Picture");
        apiPost(thetaAddr + "/osc/commands/execute", reqBody);
    }

    public void ThetaSavePicture()
    {
        //THETAのfingerprint(更新状況)が変わるまでループ
        Console.WriteLine("THETA 状態更新待ち");
        do
        {
            System.Threading.Thread.Sleep(100); //100ms待機
            //reqBody = 空
            reqBody = new PostReqBody();
            stateJson = apiPost(thetaAddr + "/osc/state", reqBody);
            nowState = JsonConvert.DeserializeObject<OscStateRes>(stateJson);
            //fingerprintが変わっていない、または_latestFileUrlが変化なしまたは空である限りループ
            if (nowState.fingerprint != preState.fingerprint
                && nowState.state._latestFileUrl != preState.state._latestFileUrl
                && nowState.state._latestFileUrl != "") break;
        }
        while (true);
        Console.WriteLine("THETA 状態更新終了");
        Console.WriteLine("THETA 画像保存");

        string originUrl = nowState.state._latestFileUrl;
        string destinationFolder; //スラッシュを含む行き先フォルダ
        string fileName; //拡張子を含むファイル名
        string filePath; //destinationFolder + fileName

        string temp = Environment.CurrentDirectory;//app/bin/debug
        temp = Directory.GetParent(temp).FullName;  //app/bin
        temp = Directory.GetParent(temp).FullName + "/root/";  //app
        destinationFolder = temp;
        fileName = "thetaImage.JPEG";
        filePath = destinationFolder + fileName;

        //THETAストレージ -> 指定ファイルパスにファイルのコピー
        using (WebClient wc = new WebClient())
        {
            wc.DownloadFile(originUrl, filePath);
        }
        Console.WriteLine("THETA save Image : " + filePath);
    }

    public void UsbCameraConnect()
    {
        DeviceInterface deviceInterface = DeviceInterface.USB;
        List<CameraDevice> detectedCameraDevices = CameraDeviceDetector.Detect(deviceInterface);//カメラインスタンスリスト
        if (detectedCameraDevices.Count == 0)
        {
            Console.WriteLine("Device has not found.");//デバイスがない
            return;
        }
        camera = detectedCameraDevices.First();//カメラインスタンス
        if (camera == null)
        {
            Console.WriteLine("Camera has not found.");//カメラがない
            return;
        }
        if (camera.EventListeners.Count == 0)
        {
            camera.EventListeners.Add(new UserEventListener());//初期実行のとき、eventリスナーを追加
        }
        var response = camera.Connect(DeviceInterface.USB);//カメラコネクト
        if (response.Equals(Response.OK))
        {
            //レスポンスが正常
            Console.WriteLine("Connected. Model: " + camera.Model + ", SerialNumber:" + camera.SerialNumber);
        }
        else
        {
            Console.WriteLine("Connection is failed.");
        }
    }

    public void UsbCameraCapture(bool WithFocus = true)
    {
        Console.WriteLine("USB Camera Take Picture");
        var response = camera.StartCapture(WithFocus); //カメラ撮影

        if (response.Result != Result.OK) //error分岐
        {
            Console.WriteLine("camera.StartCaptureメソッド is failed");
            foreach (var err in response.Errors)
            {
                Console.WriteLine("Error message : " + err);
            }
            return;
        }
    }

    public void UsbCameraDisconnect()
    {
        camera.Disconnect(DeviceInterface.USB);
        Console.WriteLine("Disconnected.");
    }

    public void Main()
    {
        //一連の動作をすべて実行
        Console.WriteLine("USB CAMERA and THETA application start"); 

        //CamerasControlを作成
        var camerasControl = new CamerasControl();

        //USBカメラ接続
        Console.WriteLine("\nUsbCameraConnect");
        camerasControl.UsbCameraConnect();

        //THETA 基本情報取得
        Console.WriteLine("\nTHETA ConnectTest");
        camerasControl.ThetaConnectTest();

        //THETA 撮影前の状態取得
        Console.WriteLine("\nTHETA GetState");
        camerasControl.ThetaGetState();

        //THETA 静止画撮影、保存(同期)
        //camerasControl.ThetaTakePicture();
        //camerasControl.ThetaSavePicture();
        //USBカメラ撮影(同期)
        //camerasControl.UsbCameraCapture(true);

        //USBカメラ画像保存はEventListenerから実行
        //カメラの画像保存動作に遅延があるため、保存する関数を実行するタイミングが難しい


        var usbCameraTask = Task.Run(() => {
            //THETA 静止画撮影、保存(非同期)
            Console.WriteLine("\nTHETA ThetaTakeAndSavePicture");
            camerasControl.ThetaTakePicture();
            camerasControl.ThetaSavePicture();
        });

        var thetaTask = Task.Run(() => {
            //USBカメラ撮影,focusなし->引数をfalseに(非同期)
            Console.WriteLine("\nUsbCamera TakeAndSavePicture");
            camerasControl.UsbCameraCapture(false);
        });

        Task.WaitAll(usbCameraTask, thetaTask);//全てのTaskが完了した時に完了扱いになるまで待つ

        //USBカメラ切断
        Console.WriteLine("\nUsbCameraDisconnect");
        camerasControl.UsbCameraDisconnect();

        Console.WriteLine("\nUSB CAMERA and THETA application end");
    }
}
