namespace AironControl.WinUI;

class Program
{
    [global::System.STAThread]
    static void Main(string[] args)
    {
        global::WinRT.ComWrappersSupport.InitializeComWrappers();
        global::Microsoft.UI.Xaml.Application.Start((p) => new App());
    }
}
