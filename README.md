# 🎓 Online Eğitim Platformu

Bu proje, **C#** kullanılarak geliştirilen bir **Online Eğitim Platformu** uygulamasıdır. Öğrenci ve öğretmen rolleri için ayrı paneller içerir ve kullanıcıların kurslara erişmesini, kurs detaylarını görüntülemesini ve içeriklerle etkileşim kurmasını sağlar.

---

## 🚀 Kurulum ve Başlatma

Projeyi çalıştırmak için aşağıdaki adımları takip edin:

1. **Gereksinimler:**
   - .NET 10 SDK ve bir **SQL Server** örneği (ör. SQL Server Express).

2. **Bağlantı Ayarını Yapın:**
   - `src/OnlineEgitim.UI/appsettings.example.json` dosyasını aynı klasörde `appsettings.json` adıyla kopyalayıp bağlantı cümlesindeki **sunucu adını** kendi ortamınıza göre düzenleyin
     (bu dosya git'e eklenmez). Alternatif olarak `EGITIM_CONNECTION` ortam değişkenini veya `dotnet user-secrets` kullanabilirsiniz.

3. **Projeyi Başlatın:**
   - `dotnet run --project src/OnlineEgitim.UI` komutunu çalıştırın ya da çözümü Visual Studio ile açın.
   - Veritabanı ilk çalıştırmada EF Core migration'larıyla otomatik oluşturulur. Eski uygulamanın veritabanı varsa veriler korunarak yerinde yükseltilir.
   - Yeni bir veritabanında hazır hesap yoktur: ilk açılışta **İlk Kurulum** ekranında ilk öğretmen hesabını kendi belirlediğiniz şifreyle oluşturun.
---

## 👨‍🎓 Öğrenci Özellikleri

- Yeni hesap oluşturma
- Sisteme giriş yapma
- Aktif kursları görüntüleme
- Kurslara katılma
- Kurs detaylarını görme
- Katıldığı kursları listeleme

### 📸 Ekran Görüntüleri (Öğrenci)

- **Giriş Ekranı:**  
  ![Öğrenci Giriş](screenshots/ogrenci_giris.png)

- **Öğrenci Paneli:**  
  ![Öğrenci Panel](screenshots/ogrenci_panel.png)

- **Kurs Detayları:**  
  ![Kurs Detayları](screenshots/kurs_detaylari.png)

- **Kurslarım:**  
  ![Kurslarım](screenshots/kurslarim.png)

---

## 👨‍🏫 Öğretmen Özellikleri

- Giriş yapma
- Yeni kurs ekleme
- Kurslara not ekleme
- Kursuna katılan öğrencileri listeleme

### 📸 Ekran Görüntüleri (Öğretmen)

- **Giriş Ekranı:**  
  ![Öğretmen Giriş](screenshots/ogretmen_giris.png)

- **Öğretmen Paneli:**  
  ![Öğretmen Paneli](screenshots/ogretmen_panel.png)

---

## 🛠️ Kullanılan Teknolojiler

- C# (.NET Framework)
- Windows Forms
- SQL Server (Veritabanı)
- ADO.NET (Veri erişimi)

---

## 📬 İletişim

Herhangi bir soru veya katkı için iletişime geçmekten çekinmeyin!

---

**Not:** Bu proje bir eğitim amaçlı çalışmadır ve daha ileri düzey özelliklerle geliştirilmeye açıktır.
