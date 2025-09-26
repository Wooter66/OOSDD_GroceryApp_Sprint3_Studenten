using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grocery.App.Views;
using Grocery.Core.Interfaces.Services;
using Grocery.Core.Models;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Linq;

namespace Grocery.App.ViewModels
{
    [QueryProperty(nameof(GroceryList), nameof(GroceryList))]
    public partial class GroceryListItemsViewModel : BaseViewModel
    {
        private readonly IGroceryListItemsService _groceryListItemsService;
        private readonly IProductService _productService;
        private readonly IFileSaverService _fileSaverService;

        private List<Product> _allProducts = new();

        public ObservableCollection<GroceryListItem> MyGroceryListItems { get; set; } = new();
        public ObservableCollection<Product> AvailableProducts { get; set; } = new();

        [ObservableProperty]
        private string searchText = string.Empty;

        public ObservableCollection<string> SortOptions { get; } =
            new ObservableCollection<string> { "A - Z", "Z - A", "Voorraad Oplopend", "Voorraad Aflopend" };

        [ObservableProperty]
        private string selectedSortOption = "A - Z";

        public IRelayCommand<string> SearchCommand { get; }

        [ObservableProperty]
        GroceryList groceryList = new(0, "None", DateOnly.MinValue, "", 0);
        [ObservableProperty]
        string myMessage;

        public GroceryListItemsViewModel(IGroceryListItemsService groceryListItemsService, IProductService productService, IFileSaverService fileSaverService)
        {
            _groceryListItemsService = groceryListItemsService;
            _productService = productService;
            _fileSaverService = fileSaverService;

            SearchCommand = new RelayCommand<string>(OnSearch);
            Load(groceryList.Id);
        }

        private void Load(int id)
        {
            MyGroceryListItems.Clear();
            foreach (var item in _groceryListItemsService.GetAllOnGroceryListId(id)) MyGroceryListItems.Add(item);
            GetAvailableProducts();
        }

        private void GetAvailableProducts()
        {
            _allProducts = _productService.GetAll()
                .Where(p => MyGroceryListItems.All(g => g.ProductId != p.Id) && p.Stock > 0)
                .ToList();

            ApplyFiltersAndSort();
        }

        private void OnSearch(string searchText)
        {
            SearchText = searchText ?? string.Empty;
            ApplyFiltersAndSort();
        }

        partial void OnSelectedSortOptionChanged(string value)
        {
            ApplyFiltersAndSort();
        }

        private void ApplyFiltersAndSort()
        {
            IEnumerable<Product> filteredProducts = _allProducts;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filteredProducts = filteredProducts
                    .Where(p => p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            switch (SelectedSortOption)
            {
                case "A - Z":
                    filteredProducts = filteredProducts.OrderBy(p => p.Name);
                    break;
                case "Z - A":
                    filteredProducts = filteredProducts.OrderByDescending(p => p.Name);
                    break;
                case "Voorraad Oplopend":
                    filteredProducts = filteredProducts.OrderBy(p => p.Stock);
                    break;
                case "Voorraad Aflopend":
                    filteredProducts = filteredProducts.OrderByDescending(p => p.Stock);
                    break;
            }

            AvailableProducts.Clear();
            foreach (var product in filteredProducts)
            {
                AvailableProducts.Add(product);
            }
        }

        partial void OnGroceryListChanged(GroceryList value)
        {
            Load(value.Id);
        }

        [RelayCommand]
        public async Task ChangeColor()
        {
            Dictionary<string, object> paramater = new() { { nameof(GroceryList), GroceryList } };
            await Shell.Current.GoToAsync($"{nameof(ChangeColorView)}?Name={GroceryList.Name}", true, paramater);
        }
        [RelayCommand]
        public void AddProduct(Product product)
        {
            if (product == null) return;
            GroceryListItem item = new(0, GroceryList.Id, product.Id, 1);
            _groceryListItemsService.Add(item);
            product.Stock--;
            _productService.Update(product);
            AvailableProducts.Remove(product);
            OnGroceryListChanged(GroceryList);
        }

        [RelayCommand]
        public async Task ShareGroceryList(CancellationToken cancellationToken)
        {
            if (GroceryList == null || MyGroceryListItems == null) return;
            string jsonString = JsonSerializer.Serialize(MyGroceryListItems);
            try
            {
                await _fileSaverService.SaveFileAsync("Boodschappen.json", jsonString, cancellationToken);
                await Toast.Make("Boodschappenlijst is opgeslagen.").Show(cancellationToken);
            }
            catch (Exception ex)
            {
                await Toast.Make($"Opslaan mislukt: {ex.Message}").Show(cancellationToken);
            }
        }
    }
}
