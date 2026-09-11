# HƯỚNG DẪN TÍCH HỢP UI TỪ CANVA & MEDIA VÀO MENU GAME

Hệ thống Menu đã được lập trình hoàn chỉnh với đầy đủ hiệu ứng phong cách Genshin Impact (Video nền động, âm nhạc huyền ảo, hiệu ứng thở "TAP TO BEGIN" và hiệu ứng phóng vào cổng trắng xóa khi mở game).

Dưới đây là hướng dẫn để bạn dễ dàng thay thế hình ảnh Canva và video/nhạc của bạn:

---

### 1. Xuất hình ảnh từ Canva
Khi thiết kế trên Canva, bạn lưu ý xuất dưới định dạng **PNG** và tích chọn **Nền trong suốt (Transparent Background)**:

| Thành phần | Kích thước gợi ý | Vị trí thay thế trong Unity |
|:---|:---|:---|
| **Logo Game** | 800 x 200 px (hoặc 1000 x 300 px) | Kéo vào `Canvas_GenshinMenu > MenuUI_Container > Logo_Area` (ô `Source Image`) |
| **Nút / Chữ Start Game** | 500 x 120 px (nếu vẽ chữ nghệ thuật) | Thay thế `TapToBegin_Text` hoặc kéo vào `Center_ActionArea` |
| **Thanh Mode / Server** | 450 x 60 px (khung viền bo góc) | Kéo vào `Server_ModeBar` (ô `Source Image`) |

> **Mẹo**: Khi kéo ảnh PNG vào Unity (thư mục `Assets/MenuAssets/UI/`), bạn hãy chọn ảnh đó trong thẻ Inspector và đổi **Texture Type** thành **Sprite (2D and UI)** rồi bấm **Apply** để có thể gán vào Image UI.

---

### 2. Video nền động & Âm thanh Genshin

1. **Video nền động (Mây / Cánh cổng)**:
   - Tải file video mây/cổng trời Genshin định dạng `.mp4` (1080p, 60fps hoặc 30fps).
   - Đặt vào thư mục `Assets/MenuAssets/Video/`.
   - Trong Unity, chọn GameObject `MenuController_Manager` $\rightarrow$ Kéo file `.mp4` vào ô **Clip** của component **Video Player**.

2. **Nhạc nền (BGM)**:
   - Chuẩn bị file nhạc nền Genshin OST (Login Theme) định dạng `.mp3` hoặc `.wav`.
   - Đặt vào `Assets/MenuAssets/Audio/`.
   - Chọn `MenuController_Manager` $\rightarrow$ Component **Audio Source** đầu tiên (BGM) $\rightarrow$ Kéo nhạc vào ô **AudioClip**.

3. **Âm thanh mở cổng đăng nhập (SFX)**:
   - File âm thanh tiếng chuông / gió lốc ánh sáng khi ấn mở cổng.
   - Chọn `MenuController_Manager` $\rightarrow$ Kéo file âm thanh vào ô **Portal Open Sfx** của component `MenuController`.

---

### 3. Cách khởi tạo tự động 1-Click
Trên thanh menu trên cùng của Unity Editor, bạn chỉ cần bấm:
👉 **Tools** $\rightarrow$ **Genshin Menu** $\rightarrow$ **1-Click Setup Menu Scene**

Hệ thống sẽ tự động:
- Mở và thiết lập toàn bộ Scene `Menu.unity`.
- Khởi tạo Canvas chuẩn 1920x1080, Video Player, Audio Sources, Nút tương tác và hiệu ứng White Flash.
- Kết nối toàn bộ code điều khiển hoàn toàn tự động!
