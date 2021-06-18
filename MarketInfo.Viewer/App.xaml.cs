using MarketInfo.Viewer.Views;
using Prism.Unity;
using Prism.Ioc;
using System.Windows;

namespace MarketInfo.Viewer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : PrismApplication
    {
        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterInstance<StockPriceService>(new FinnhubStockPriceService("REDACTED"));
        }
    }
}
