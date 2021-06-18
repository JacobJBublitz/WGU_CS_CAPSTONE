#nullable enable

using MarketInfo.Viewer.Events;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Linq;

namespace MarketInfo.Viewer.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly StockPriceService _stockPriceService;
        private readonly IEventAggregator _eventAggregator;

        private string _searchText = "";
        public string SearchText
        {
            get { return _searchText; }
            set { SetProperty(ref _searchText, value); }
        }

        private IEnumerable<string> _symbols = new List<string>();

        private IEnumerable<string> _filteredSymbols = new List<string>();
        public IEnumerable<string> FilteredSymbols
        {
            get { return _filteredSymbols; }
            set { SetProperty(ref _filteredSymbols, value); }
        }

        private string? _selectedSymbol = null;
        public string? SelectedSymbol
        {
            get { return _selectedSymbol; }
            set { 
                SetProperty(ref _selectedSymbol, value);
                if (value != null)
                {
                    _eventAggregator.GetEvent<TickerSymbolSelectedEvent>().Publish(value);
                }
            }
        }

        public DelegateCommand RefreshSymbolsDelegateCommand { get; private set; }
        public DelegateCommand<string> FilterSymbolsCommand { get; private set; }

        public MainWindowViewModel(IEventAggregator ea, StockPriceService stockPriceService)
        {
            _eventAggregator = ea;
            _stockPriceService = stockPriceService;

            RefreshSymbolsDelegateCommand = new DelegateCommand(RefreshSymbols);
            FilterSymbolsCommand = new DelegateCommand<string>(FilterSymbols);
        }

        private async void RefreshSymbols()
        {
            var symbols = new SortedSet<string>();

            await foreach (var sym in _stockPriceService.GetSymbolsAsync())
            {
                if (sym.All(char.IsLetter))
                    symbols.Add(sym);
            }

            _symbols = symbols;
            FilterSymbols(SearchText); // Update filtered symbols
        }

        private void FilterSymbols(string filterString)
        {
            var upperFilterString = filterString.ToUpper();

            
            if (filterString.Length == 0)
                FilteredSymbols = _symbols;
            else
                FilteredSymbols = _symbols.AsParallel().Where(sym => sym.StartsWith(upperFilterString)).AsEnumerable();
        }
    }
}
