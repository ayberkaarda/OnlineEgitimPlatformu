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
     Kimliği belirsiz kayıtlar (eksik, boşluklu veya yinelenen ad/e-posta, OgrenciID ve DersID dışında sütun içeren kayıt anahtarı, mükerrer ders kaydı)
     yükseltmeyi hiçbir değişiklik yapmadan durdurur; hata penceresi ve günlük yalnızca sorun türünü ve sayısını gösterir. Kayıtları düzeltip uygulamayı yeniden başlatın.

4. **İlk Öğretmen (Kurucu Anahtarı):**
   - Hazır hesap yoktur. Veritabanı tamamen boşsa uygulama **İlk Kurulum** ekranıyla açılır; öğrencileri olan ama öğretmeni olmayan bir veritabanında
     normal giriş ekranı açılır ve kurulum, girişteki **İlk öğretmen kurulumu (kurucu anahtarı)** bağlantısındadır.
   - İlk öğretmen yalnızca bu bilgisayarın **kurulum anahtarıyla** oluşturulur. Anahtar, ekran açıldığında `%LOCALAPPDATA%\OnlineEgitim\setup-key.txt`
     dosyasına rastgele yazılır (uygulamayı çalıştıran Windows kullanıcısının profilinde). Alternatif olarak en az 16 karakterlik `EGITIM_SETUP_KEY` ortam değişkeni
     tanımlanabilir. Anahtar bir kez kullanılır, kullanılınca geçersiz olur ve hiçbir yere kaydedilmez/günlüğe yazılmaz.

5. **Şifresiz Eski Hesapların Kurtarılması:**
   - Eski uygulamada şifresi boş kalan hesaplar boş veya herhangi bir şifreyle giriş yapamaz; hesap kimliği ve ders kayıtları korunarak şifre şöyle belirlenir:
   - **Öğrenci:** Öğretmen panelindeki **Kurtarma Kodu Üret** ile öğrencinin adına tek kullanımlık, 24 saat geçerli bir kod üretilir (veritabanında yalnızca
     özeti tutulur). Öğrenci girişteki **Şifresiz hesabı kurtar** bağlantısında adını, kodu ve yeni şifresini girer. Hatalı kodlar giriş kilidine sayılır.
   - **Öğretmen:** Aynı bağlantıda **Öğretmen (kurulum anahtarı)** seçilir; şifresiz öğretmen hesabına kurucu anahtarıyla bir kez şifre belirlenir.
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
