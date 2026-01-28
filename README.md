# Ezan Vakti / Prayer Times

Windows platformu için geliştirilmiş, sistem kaynaklarını minimum düzeyde kullanan, modern ve şık bir namaz vakitleri masaüstü uygulamasıdır.

A lightweight, modern, and performance-oriented prayer times desktop application developed for the Windows platform.

---

## Türkçe

### Genel Bakış
Ezan Vakti, Windows 10 ve 11 işletim sistemleri ile tam uyumlu, sistem tepsisi (system tray) üzerinden yönetilen ve ekranda şık bir overlay paneli sunan bir yardımcı araçtır. Uygulama, Windows Konum Servisleri'ni kullanarak veya manuel şehir seçimi ile doğru vakit bilgilerini sağlar.

### Öne Çıkan Özellikler
- **Sistem Tepsisi Entegrasyonu:** Uygulama arka planda sessizce çalışır, tüm kontrollere sağ tık menüsü ile erişilir.
- **Dinamik Konum Desteği:** Windows Konum Servisleri aracılığıyla otomatik konum belirleme veya Türkiye'deki tüm iller için manuel seçim.
- **Akıllı Veri Yönetimi:** Vakit bilgileri aylık olarak çekilir ve yerel olarak depolanır. Bu sayede her açılışta ağ trafiği oluşturmaz ve performans kaybını önler.
- **Modern Arayüz:** WPF teknolojisi kullanılarak hazırlanan, ekranın istenilen noktasında konumlanabilen kompakt bir overlay.
- **Düşük Kaynak Tüketimi:** Bellek ve işlemci kullanımı "idle" durumunda minimum seviyeye indirilmiştir.

### Teknik Detaylar
- **Platform:** .NET 6.0 (WPF)
- **Minimum OS:** Windows 10 (Build 17763)
- **Kütüphaneler:** Hardcodet.Wpf.NotifyIcon, Windows SDK entegrasyonu.
- **Yayınlama Biçimi:** Self-contained, single-file executable.

### Kurulum ve Kullanım
1. Projenin `Installer` dizinindeki kurulum dosyasını çalıştırın.
2. Uygulama başladığında sistem tepsisindeki ikon üzerinden ayarlarınıza erişin.
3. Konum servislerini aktif edebilir veya manuel olarak şehrinizi seçebilirsiniz.

---

## English

### Overview
Ezan Vakti is a utility tool fully compatible with Windows 10 and 11, managed via the system tray and featuring a sleek overlay panel. It provides accurate prayer times using Windows Location Services or manual city selection.

### Key Features
- **System Tray Integration:** Runs silently in the background; all controls are accessible via the right-click tray menu.
- **Dynamic Location Support:** Automatic positioning through Windows Location Services or manual selection for all cities in Turkey.
- **Intelligent Data Management:** Prayer data is fetched monthly and stored locally. This eliminates redundant network requests on startup and optimizes performance.
- **Modern Interface:** A compact overlay built with WPF technology that can be positioned anywhere on the screen.
- **Efficient Resource Usage:** Memory and CPU footprint are minimized for idle state performance.

### Technical Stack
- **Platform:** .NET 6.0 (WPF)
- **Minimum OS:** Windows 10 (Build 17763)
- **Libraries:** Hardcodet.Wpf.NotifyIcon, Windows SDK integration.
- **Distribution:** Self-contained, single-file executable.

### Installation and Usage
1. Run the setup file found in the `Installer` directory.
2. Once launched, access your settings via the icon in the system tray.
3. Enable location services or select your city manually from the menu.

---

## Geliştirme / Development

Projeyi yerel ortamda derlemek için Visual Studio 2022 ve .NET 6 SDK gereklidir.

To build the project locally, Visual Studio 2022 and .NET 6 SDK are required.

```powershell
# Projeyi derleme / Build project
dotnet build -c Release

# Yayınlama paketi oluşturma / Create publish package
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Lisans / License
Bu proje MIT lisansı altında korunmaktadır.
This project is licensed under the MIT License.
