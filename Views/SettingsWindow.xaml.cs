using System.Windows;
using System.Windows.Controls;
using EzanVakti.Models;
using EzanVakti.Services;

namespace EzanVakti.Views;

public partial class SettingsWindow : Window
{
    private CityInfo? _selectedCity;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadCities();
        UpdateCurrentCityDisplay();
    }

    private void LoadCities()
    {
        var cities = TurkishCities.GetAllCities().ToList();
        CityComboBox.ItemsSource = cities;
        CityComboBox.DisplayMemberPath = "Name";
        
        // Select current city
        var currentCity = ConfigService.Instance.Config.City;
        var city = cities.FirstOrDefault(c => c.Name == currentCity);
        if (city != null)
        {
            CityComboBox.SelectedItem = city;
            _selectedCity = city;
        }
    }

    private void UpdateCurrentCityDisplay()
    {
        CurrentCityText.Text = ConfigService.Instance.GetLocationDisplay();
    }

    private void CityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CityComboBox.SelectedItem is CityInfo city)
        {
            _selectedCity = city;
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCity == null)
        {
            MessageBox.Show("Lütfen bir şehir seçin.", "Uyarı", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConfigService.Instance.UpdateCity(_selectedCity.Name);
        await ConfigService.Instance.SaveAsync();
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
