using System.Runtime.InteropServices;

Environment.SetEnvironmentVariable("SteamAppId", "480");
var dll = NativeLibrary.Load(@"c:\program files (x86)\steam\steamclient64.dll");
var createInterface = Marshal.GetDelegateForFunctionPointer<CreateInterfaceDelegate>(NativeLibrary.GetExport(dll, "CreateInterface"));
var createPipe = Marshal.GetDelegateForFunctionPointer<CreatePipeDelegate>(NativeLibrary.GetExport(dll, "Steam_CreateSteamPipe"));
var connectUser = Marshal.GetDelegateForFunctionPointer<ConnectUserDelegate>(NativeLibrary.GetExport(dll, "Steam_ConnectToGlobalUser"));
var error = 0;
var client = createInterface("SteamClient017", ref error);
var pipe = createPipe();
var user = connectUser(pipe);
var friends = Slot<GetInterfaceDelegate>(client, 8)(client, pipe, user, "SteamFriends017");
var inner = Marshal.ReadIntPtr(friends, 8);
var table = Marshal.ReadIntPtr(inner);
for (var slot = 0; slot < 40; slot++)
{
    var target = Marshal.ReadIntPtr(table, slot * 8);
    var bytes = new byte[8];
    Marshal.Copy(target, bytes, 0, bytes.Length);
    Console.WriteLine($"[{slot}] {target - dll:X} {Convert.ToHexString(bytes)}");
}

static T Slot<T>(nint self, int index) where T : Delegate
    => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), index * 8));

delegate nint CreateInterfaceDelegate(string version, ref int returnCode);
delegate int CreatePipeDelegate();
delegate int ConnectUserDelegate(int pipe);
delegate nint GetInterfaceDelegate(nint self, int pipe, int user, string version);
