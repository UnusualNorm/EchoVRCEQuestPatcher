using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using Newtonsoft.Json.Linq;
using QuestPatcher.Zip;
using BsDiff;

namespace EchoVRCEQuestPatcher
{
    internal class Program
    {
        public const string APK_HASH = "21c7dc914dba2fa44f8daf019aedffa4a17e14186283ef05805217fc80d30eaf";
        public const string OBB_HASH = "8020d1791c8806b3c9592806d2476c9b9c9771cbfcbce112914b357b66180607";
        public const string ADB_DOWNLOAD = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";

        public static readonly string ADB_ROOT = Path.Join(Path.GetTempPath(), "echovrcequestpatcherplatformtools");
        public static readonly string ADB_DIR = Path.Join(ADB_ROOT, "platform-tools");
        public static readonly string ADB_PATH = Path.Join(ADB_DIR, "adb.exe");

        class SimpleShellOutputReciever : IShellOutputReceiver
        {
            public bool ParsesErrors
            {
                get => false;
            }
            public string Output = "";

            public void AddOutput(string data)
            {
                Output += data + "\n";
            }

            public void Flush()
            {
            }
        }

        public static readonly string[] validProducts =
        {
            "vr_monterey",        // Oculus Quest 1
            "hollywood",         // Oculus Quest 2
            "seacliff",         // Oculus Quest Pro
            "eureka",          // Oculus Quest 3
        };

        public static readonly string[] validLibR15Hashes =
        {
            "8dd9a961b9dca8566069a4f65b3ddee9c65682c4e9c91a6d41e3c5727b1d8b20", // Legit
        };

        static string GetSha256(Stream stream)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        static string GetAPKPath(AdbClient client, DeviceData device, string package)
        {
            var shellOutput = new SimpleShellOutputReciever();
            client.ExecuteRemoteCommand($"pm path {package}", device, shellOutput);
            var output = shellOutput.Output;
            var path = output.Split(':')[1].Trim();
            return path;
        }

        static void Main()
        {
            Console.WriteLine("Echo VR: CE Quest Patcher");
            Console.WriteLine("By: @unusualnorm");
            Console.WriteLine("-------------------------");

            var process = "";
            try
            {
                if (!File.Exists(ADB_PATH))
                {
                    process = "download ADB Server";
                    Console.WriteLine($"Downloading ADB Server...");

                    if (Directory.Exists(ADB_ROOT))
                        Directory.Delete(ADB_ROOT, true);
                    Directory.CreateDirectory(ADB_ROOT);

                    var adbZipPath = Path.Join(ADB_ROOT, "platform-tools.zip");

                    {
                        using var httpClient = new HttpClient();
                        using var adbZipFile = httpClient.GetStreamAsync(ADB_DOWNLOAD).Result;
                        using var adbZipFileStream = File.OpenWrite(adbZipPath);
                        adbZipFile.CopyTo(adbZipFileStream);
                    }

                    ZipFile.ExtractToDirectory(adbZipPath, ADB_ROOT);
                    File.Delete(adbZipPath);
                }

                if (!AdbServer.Instance.GetStatus().IsRunning)
                {
                    process = "start ADB Server";
                    Console.WriteLine($"Starting ADB Server...");
                    AdbServer.Instance.StartServer(ADB_PATH, false);
                }

                process = "connect to ADB Server";
                Console.WriteLine($"Connecting to ADB Server...");
                var client = new AdbClient();
                client.Connect("127.0.0.1");

                process = "find device";
                Console.WriteLine($"Finding device...");
                DeviceData? device = null;
                while (device == null)
                {
                    var devices = client.GetDevices();
                    foreach (var d in devices)
                    {
                        if (!validProducts.Contains(d.Product)) continue;
                        device = d;
                    }

                    if (device != null) break;
                    Thread.Sleep(1000);
                }
                Console.WriteLine($"Found device: {device.Product}");

                // TODO: better naming
                process = "get Echo VR";
                Console.WriteLine($"Getting Echo VR...");
                var apkPath = "r15_goldmaster_store.apk";
                var obbPath = "main.4987566.com.readyatdawn.r15.obb";
                if (!File.Exists(apkPath) || !File.Exists(obbPath))
                {
                    process = "get Echo VR version";
                    Console.WriteLine($"Getting Echo VR version...");
                    var currentAppVersion = client.GetPackageVersion(device, "com.readyatdawn.r15");
                    Console.WriteLine("Current Echo VR version: " + currentAppVersion);

                    if (currentAppVersion != null && currentAppVersion.VersionName == "4987566")
                    {
                        process = "get Echo VR APK path";
                        Console.WriteLine($"Getting Echo VR APK path...");
                        var remoteApkPath = GetAPKPath(client, device, "com.readyatdawn.r15");
                        if (string.IsNullOrEmpty(remoteApkPath))
                            throw new Exception("Echo VR APK path is null or empty");
                        Console.WriteLine($"Echo VR APK path: {remoteApkPath}");

                        process = "get Echo VR OBB path";
                        Console.WriteLine($"Getting Echo VR OBB path...");
                        var remoteObbPath = "/sdcard/Android/obb/com.readyatdawn.r15/main.4987566.com.readyatdawn.r15.obb";
                        Console.WriteLine($"Echo VR OBB path: {obbPath}");

                        process = "download Echo VR APK";
                        Console.WriteLine($"Downloading Echo VR APK...");
                        using var apkStream = File.OpenWrite(apkPath);
                        client.Pull(device, remoteApkPath, apkStream);

                        process = "download Echo VR OBB";
                        Console.WriteLine($"Downloading Echo VR OBB...");
                        using var obbStream = File.OpenWrite(obbPath);
                        client.Pull(device, remoteObbPath, obbStream);
                    }
                    else
                    {
                        // TODO: Put other methods here
                    }
                }

                process = "extract Echo VR APK";
                var extractedApkDir = Path.Join(Path.GetTempPath(), "echovrcequestpatcherextractedapk");
                Console.WriteLine($"Extracting Echo VR APK...");
                {
                    if (Directory.Exists(extractedApkDir))
                        Directory.Delete(extractedApkDir, true);
                    Directory.CreateDirectory(extractedApkDir);

                    var apkZip = ZipFile.OpenRead(apkPath);
                    apkZip.ExtractToDirectory(extractedApkDir);
                }

                // TODO: Check better
                process = "check Echo VR APK version";
                Console.WriteLine($"Checking Echo VR APK version...");
                {
                    var libR15Path = Path.Join(extractedApkDir, "lib", "arm64-v8a", "libr15.so");
                    using var libR15Stream = File.OpenRead(libR15Path);
                    var libR15Hash = GetSha256(libR15Stream);
                    if (!validLibR15Hashes.Contains(libR15Hash))
                        throw new Exception($"Echo VR APK version is not correct! Expected: {validLibR15Hashes[0]}, Actual: {libR15Hash}");
                }

                process = "patch libpnsovr.so";
                Console.WriteLine($"Patching libpnsovr.so...");
                {
                    var libPnsOvrPath = Path.Join(extractedApkDir, "lib", "arm64-v8a", "libpnsovr.so");
                    var newLibPnsOvrPath = Path.Join(extractedApkDir, "lib", "arm64-v8a", "libpnsovr.so.new");

                    using var libPnsOvrStream = File.OpenRead(libPnsOvrPath);
                    using var newLibPnsOvrStream = File.OpenWrite(newLibPnsOvrPath);

                    var libPnsOvrHash = GetSha256(libPnsOvrStream);
                    if (Assembly.GetExecutingAssembly().GetManifestResourceInfo($"EchoVRCEQuestPatcher.libpnsovr_patch_{libPnsOvrHash}") == null)
                        throw new Exception($"libpnsovr.so patch for {libPnsOvrHash} does not exist!");

                    BinaryPatch.Apply(libPnsOvrStream, () => Assembly.GetExecutingAssembly().GetManifestResourceStream($"EchoVRCEQuestPatcher.libpnsovr_patch_{libPnsOvrHash}"), newLibPnsOvrStream);

                    libPnsOvrStream.Dispose();
                    newLibPnsOvrStream.Dispose();

                    File.Delete(libPnsOvrPath);
                    File.Move(newLibPnsOvrPath, libPnsOvrPath);
                }

                process = "insert config.json";
                Console.WriteLine($"Inserting config.json...");
                {
                    var configJsonDir = Path.Join(extractedApkDir, "assets", "_local");
                    var configJsonPath = Path.Join(configJsonDir, "config.json");

                    if (!Directory.Exists(configJsonDir))
                        Directory.CreateDirectory(configJsonDir);
                    if (File.Exists(configJsonPath))
                        File.Delete(configJsonPath);

                    using var configJsonStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EchoVRCEQuestPatcher.config.json");
                    var configJson = JObject.Parse(new StreamReader(configJsonStream!).ReadToEnd());
                    configJson["loginservice_host"] = configJson["loginservice_host"] + device.Serial;
                    File.WriteAllText(configJsonPath, configJson.ToString());
                }

                process = "remove META-INF";
                Console.WriteLine($"Removing META-INF...");
                {
                    var metaInfDir = Path.Join(extractedApkDir, "META-INF");
                    if (Directory.Exists(metaInfDir))
                        Directory.Delete(metaInfDir, true);
                }

                process = "create new Echo VR APK";
                Console.WriteLine($"Creating new Echo VR APK...");
                {
                    if (File.Exists("r15_goldmaster_store_patched.apk"))
                        File.Delete("r15_goldmaster_store_patched.apk");
                    ZipFile.CreateFromDirectory(extractedApkDir, "r15_goldmaster_store_patched.apk");
                    Directory.Delete(extractedApkDir, true);
                }

                process = "sign new Echo VR APK";
                Console.WriteLine($"Signing new Echo VR APK...");
                {
                    using var newApkStream = File.Open("r15_goldmaster_store_patched.apk", FileMode.Open);
                    ApkZip.Open(newApkStream).Dispose();
                }

                process = "uninstall Echo VR";
                Console.WriteLine($"Uninstalling Echo VR...");
                {
                    var appVersion = client.GetPackageVersion(device, "com.readyatdawn.r15");
                    if (appVersion != null)
                        client.UninstallPackage(device, "com.readyatdawn.r15");
                }

                process = "install Echo VR";
                Console.WriteLine($"Installing Echo VR...");
                {
                    using var newApkStream = File.OpenRead("r15_goldmaster_store_patched.apk");
                    newApkStream.Position = 0;
                    client.Install(device, newApkStream);
                    newApkStream.Dispose();
                    File.Delete("r15_goldmaster_store_patched.apk");
                }

                process = "install Echo VR OBB";
                Console.WriteLine($"Installing Echo VR OBB...");
                {
                    using var obbStream = File.OpenRead(obbPath);
                    obbStream.Position = 0;
                    client.ExecuteRemoteCommand($"mkdir -p /sdcard/Android/obb/com.readyatdawn.r15", device, null);
                    client.Push(device, "/sdcard/Android/obb/com.readyatdawn.r15/main.4987566.com.readyatdawn.r15.obb", obbStream, 0, new DateTimeOffset());
                }

                Console.WriteLine("-----------------------------------");
                Console.WriteLine("Successfully installed Echo VR: CE!");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to {process}!");
                Console.WriteLine(e);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }
        }
    }
}
