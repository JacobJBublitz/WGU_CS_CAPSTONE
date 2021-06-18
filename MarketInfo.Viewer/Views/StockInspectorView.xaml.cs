using OxyPlot;
using System.Windows.Controls;

namespace MarketInfo.Viewer.Views
{
    /// <summary>
    /// Interaction logic for StockInspectorView.xaml
    /// </summary>
    public partial class StockInspectorView : UserControl
    {
        public StockInspectorView()
        {
            InitializeComponent();

            var controller = new PlotController();
            controller.UnbindAll();

            PricePlot.Controller = controller;

            var auxController = new PlotController();
            auxController.UnbindAll();
            AuxilaryPlot.Controller = auxController;
        }
    }
}
