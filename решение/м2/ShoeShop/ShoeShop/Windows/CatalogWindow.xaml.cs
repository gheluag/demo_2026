
using Microsoft.EntityFrameworkCore;
using ShoeShop.DbContexts;
using ShoeShop.Entities;
using ShoeShop.Windows;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ShoeShop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class CatalogWindow : Window
    {

        private ObservableCollection<Product> Products;
        private ICollectionView ProductsView;


        public CatalogWindow()
        {
            InitializeComponent();

            LoadProdData();
            LoadUserInfo();
            ConfigInfo();
            LoadSuppliers();

        }


        private void ConfigInfo()
        {
            bool canSort = CurrentUser.Role is "Администратор" or "Менеджер";

            SortGrid.Visibility = canSort
                ? Visibility.Visible
                : Visibility.Collapsed;

            AdminPanel.Visibility = CurrentUser.Role == "Администратор"
                ? Visibility.Visible
                : Visibility.Collapsed;

        }

        private void LoadUserInfo()
        {
            UserNameTB.Text = CurrentUser.FullName;
        }

        private void LoadProdData()
        {
            using var db = new ShoeshopContext();

            Products = new ObservableCollection<Product>(
                db.Products
                  .Include(p => p.Cat)
                  .Include(p => p.ProdNameNavigation)
                  .Include(p => p.Manuf)
                  .Include(p => p.Sup)
                  .ToList()
            );

            ProductsView = CollectionViewSource.GetDefaultView(Products);
            ProductsView.Filter = FilterProduct;

            prodList.ItemsSource = ProductsView;
        }


        private void exBtn_Click(object sender, RoutedEventArgs e)
        {
            LoginWin loginWin = new();
            loginWin.Show();
            Close();
        }

        private void SearchTB_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility =
                string.IsNullOrWhiteSpace(SearchTB.Text)
                    ? Visibility.Visible
                    : Visibility.Hidden;

            ProductsView.Refresh();
        }


        private void SupplierCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void LoadSuppliers()
        {
            using (var db = new ShoeshopContext())
            {
                var suppliers = db.Suppliers.ToList();
                suppliers.Insert(0, new Supplier
                {
                    IdSup = 0,
                    SupName = "Все поставщики"
                });

                SupplierCB.ItemsSource = suppliers;
                SupplierCB.SelectedIndex = 0;
            }
        }


        private bool FilterProduct(object obj)
        {
            if (obj is not Product p)
                return false;

          
            if (!string.IsNullOrWhiteSpace(SearchTB.Text))
            {
                string text = SearchTB.Text.ToLower();

                bool found =
                    p.ProdNameNavigation.ProdName1.ToLower().Contains(text) ||
                    p.Descrip?.ToLower().Contains(text) == true ||
                    p.Cat.CatName.ToLower().Contains(text) ||
                    p.Manuf.ManufName.ToLower().Contains(text) ||
                    p.Sup.SupName.ToLower().Contains(text);

                if (!found) return false;
            }

       
            if (SupplierCB.SelectedIndex > 0)
            {
                int supId = (int)SupplierCB.SelectedValue;
                if (p.SupId != supId) return false;
            }

            return true;
        }





        private void ApplySorting()
        {
            ProductsView.SortDescriptions.Clear();


            switch (SortCB.SelectedIndex)
            {
                case 1:
                    ProductsView.SortDescriptions.Add(
                        new SortDescription(nameof(Product.Count), ListSortDirection.Ascending));
                    break;

                case 2:
                    ProductsView.SortDescriptions.Add(
                        new SortDescription(nameof(Product.Count), ListSortDirection.Descending));
                    break;
            }
        }


        private void ApplyFilters()
        {
            if (ProductsView == null)
                return;

            ProductsView.Refresh();
            ApplySorting();
        }



        private void SortCB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void AddProdBtn_Click(object sender, RoutedEventArgs e)
        {
            AddProdWindow addProdWindow = new();
            addProdWindow.ShowDialog();
            LoadProdData();
        }

        private void EditProdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (prodList.SelectedItem == null)
            {
                MessageBox.Show(
                    "Выберите товар для редактирования",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            Product selectedProduct = prodList.SelectedItem as Product;
            EditProdWindow editProd = new(selectedProduct);
            if(editProd.ShowDialog() == true) LoadProdData();

        }

        private void DelProdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (prodList.SelectedItem is not Product selectedProduct)
            {
                MessageBox.Show(
                    "Сначала выберите товар для удаления",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Вы действительно хотите удалить товар \"{selectedProduct.ProdNameNavigation.ProdName1}\"?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                using var db = new ShoeshopContext();

                var prodInDb = db.Products
                    .Include(p => p.Prodorders)
                    .First(p => p.IdProd == selectedProduct.IdProd);

                if (prodInDb.Prodorders.Any())
                {
                    MessageBox.Show(
                        "Нельзя удалить товар, который есть в заказе",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                if (!string.IsNullOrEmpty(prodInDb.Image))
                {
                    string imagesDir = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory, "Images");

                    string fullPath = Path.Combine(imagesDir, prodInDb.Image);

                    if (File.Exists(fullPath))
                    {
                        try
                        {
                            File.Delete(fullPath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                }

                db.Products.Remove(prodInDb);
                db.SaveChanges();

               
                Products.Remove(selectedProduct);

                
                ProductsView.Refresh();

                MessageBox.Show(
                    "Товар успешно удалён",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Ошибка при удалении товара: {ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }


    }
}

