# HIT-Gigachad

HIT-Gigachad là game **survival top-down, auto-battle roguelite** được phát triển bằng Unity. Người chơi điều khiển Gigachad trên một đấu trường sa mạc, chiến đấu với các đợt quái ngày càng đông, thu thập XP, nâng cấp vũ khí và chọn các sách buff để sống sót lâu nhất có thể.

Dự án lấy cảm hứng từ:

- **MegaBonk:** gameplay survival top-down, vũ khí tự động và horde enemy.
- **Vampire Survivors:** hệ thống lựa chọn vũ khí và passive sau mỗi lần lên cấp.
- **Brotato:** thiết kế vũ khí đa dạng và cân bằng chỉ số.

## Gameplay

Một vòng chơi hoàn chỉnh gồm:

1. Mua nâng cấp meta bằng Silver tại Shop.
2. Chọn nhân vật và bản đồ trong menu chính.
3. Di chuyển, né đòn và chiến đấu với các đợt quái liên tục.
4. Thu thập XP Gem để lên cấp.
5. Chọn một trong ba nâng cấp vũ khí hoặc tome ngẫu nhiên.
6. Đối đầu với raid wave và boss xuất hiện định kỳ.
7. Nhận Silver sau khi kết thúc để chuẩn bị cho lượt chơi tiếp theo.

Vũ khí tự động kích hoạt nên người chơi tập trung vào di chuyển, né kỹ năng và xây dựng bộ nâng cấp phù hợp.

## Tính năng chính

### Nhân vật và chỉ số

Mỗi nhân vật sử dụng một `CharacterData` ScriptableObject để lưu các chỉ số cơ bản như HP, Shield, Attack, Speed, Defense và Jump. `PlayerBaseStats` kết hợp chỉ số gốc với các bonus nhận được trong trận để tính chỉ số cuối cùng.

Shield hấp thụ sát thương trước, phần sát thương còn lại mới trừ vào HP.

### Hệ thống vũ khí

Dự án hiện có 6 vũ khí:

- Sword
- Aura
- Arrow
- Revolver
- Firewalker
- FireBall

Các vũ khí được xây dựng từ `WeaponData` ScriptableObject và bốn nhóm hành vi chính:

| Hành vi | Mô tả |
|---|---|
| `MeleeWeapon` | Quét hitbox theo hướng nhân vật và hỗ trợ combo |
| `ProjectileWeapon` | Bắn về phía enemy gần nhất, hỗ trợ xuyên mục tiêu và phân tách đạn |
| `AoEWeapon` | Tạo vùng sát thương tại vị trí ngẫu nhiên hoặc trên player |
| `AuraWeapon` | Gây sát thương liên tục quanh player theo chu kỳ |

Mỗi cấp vũ khí cải thiện các thuộc tính thông qua `AutomaticWeaponUpgradeStats`.

### Tome và nâng cấp bị động

Có 8 loại tome: Damage, Size, Speed, Health, Armor, Cooldown, Projectile Speed và XP. Mỗi tome là một ScriptableObject, cung cấp bonus cố định hoặc theo phần trăm và áp dụng trực tiếp vào chỉ số người chơi.

Khi đủ XP, game tạm dừng và hiển thị ba lựa chọn ngẫu nhiên gồm vũ khí hoặc tome.

### Boss

- **Stone Golem:** sử dụng Sand Burst, Seismic Ring và đòn đánh cận chiến.
- **Anubis:** sử dụng Red Tracking Laser bám theo người chơi và Blue Laser Ring gồm tám tia xoay phủ 360 độ.

Các kỹ năng boss có giai đoạn cảnh báo trước khi gây sát thương. Một số đòn có thể né bằng cách nhảy đúng thời điểm.

### Các hệ thống khác

- **Chest System:** rương xuất hiện trên bản đồ, có animation mở và trao vũ khí hoặc tome ngẫu nhiên.
- **Shop System:** mua thêm Weapon Slot, Tome Slot, Reroll, Skip và Remove bằng Silver.
- **Meta-progression:** trạng thái nâng cấp được lưu bằng `PlayerPrefs`, giá tăng theo số lần mua.
- **Audio Manager:** quản lý nhạc nền và hiệu ứng âm thanh tập trung.
- **Damage Number Popup:** hiển thị sát thương khi đánh trúng enemy.
- **Stable Frame Pacing:** giới hạn tốc độ khung hình theo tần số quét màn hình, tối đa 120 FPS.

## AI và tối ưu hiệu năng

### Flow-field pathfinding

Enemy dùng chung một flow field thay vì tự tìm đường riêng lẻ. Grid có kích thước 160 × 160 ô, mỗi ô rộng 1 m và tự dịch chuyển, xây dựng lại khi player đi quá xa tâm grid.

Quá trình tạo flow field gồm:

1. Tạo cost field bằng cách kiểm tra vật cản.
2. Dùng BFS để xây dựng integration field từ vị trí player.
3. Cho mỗi ô chọn hướng về ô lân cận có chi phí thấp nhất.

### Jobs System và Burst Compiler

`EnemyManager` đưa dữ liệu enemy đang hoạt động vào `NativeArray`, sau đó dùng `IJobParallelForTransform` để tính toán chuyển động song song trên các worker thread. Spatial hashing bằng `NativeParallelMultiHashMap` tạo lực tách, hạn chế enemy chồng lên nhau.

Kiến trúc này cho phép xử lý hơn 300 enemy cùng lúc trong khi giảm tải cho main thread.

### AI LOD và raycast budget

Tần suất cập nhật AI thay đổi theo khoảng cách tới player:

| Khoảng cách | Cập nhật hướng | Kiểm tra môi trường |
|---|---:|---:|
| Dưới 10 m | 0,05 giây | 0,08 giây |
| 10–25 m | 0,12 giây | 0,18 giây |
| Trên 25 m | 0,35 giây | 0,50 giây |

Environment check được giới hạn tối đa 48 raycast mỗi frame và ưu tiên enemy ở gần player.

### Spawn system

- Enemy spawn theo nhóm và ưu tiên phía trước hướng nhìn của player.
- Kích thước nhóm tăng dần theo thời gian sống sót.
- Enemy mạnh hơn bắt đầu xuất hiện từ phút đầu tiên.
- Mỗi 4 phút có một raid wave kéo dài 60 giây.
- Enemy có animation trồi lên từ dưới đất khi xuất hiện.

### Tối ưu animation và môi trường

Dự án sử dụng ba phương pháp animation cho enemy:

- **Mesh Flipbook:** bake animation thành nhiều mesh rồi đổi frame theo thời gian.
- **Vertex Animation Texture (VAT):** chạy animation vertex hoàn toàn trên GPU.
- **Hybrid LOD Animator:** dùng Animator thật ở gần và chuyển sang VAT hoặc Flipbook khi ở xa.

Props trong môi trường được chia thành các spatial chunk 24 × 24 m. Mỗi frame chỉ đánh giá một phần chunk; renderer ngoài tầm nhìn bị tắt trong khi collider và gameplay object vẫn hoạt động.

## Công nghệ sử dụng

| Công nghệ | Vai trò |
|---|---|
| Unity 6000.3.10f1 + URP | Engine và render pipeline |
| Unity Jobs System + Burst Compiler | Tính toán chuyển động enemy song song |
| Unity Input System | Xử lý input người chơi |
| Unity ObjectPool | Tái sử dụng enemy, XP Gem và projectile |
| Unity Playables API | Animation pipeline cho Hybrid LOD |
| DOTween | Tween UI và hiệu ứng animation |
| Spine Runtime | Animation cho một số nhân vật UI |
| Meshy AI | Hỗ trợ tạo model 3D từ ảnh |

## Cấu trúc dự án

| Đường dẫn | Nội dung |
|---|---|
| `Assets/001Scripts/Enemy/` | Flow field, enemy AI, spawn, health và boss attack |
| `Assets/001Scripts/Player/` | Movement, health, stats, character data và XP |
| `Assets/001Scripts/Weapon/` | Weapon controller, inventory, behaviour, VFX và upgrade |
| `Assets/001Scripts/Tome/` | Tome data và tome inventory |
| `Assets/001Scripts/Rendering/` | VAT, Mesh Flipbook và Hybrid LOD Animator |
| `Assets/001Scripts/Performance/` | Tối ưu render môi trường và frame pacing |
| `Assets/001Scripts/Audio/` | Audio manager và UI sound effect |
| `Assets/Editor/` | Các công cụ editor hỗ trợ thiết lập prefab |
| `Assets/Resources/Weapons/` | Các `WeaponData` asset |
| `Assets/Resources/Tomes/` | Các `TomeData` asset |
| `Assets/UI/UIStart/` | UI menu, shop, chọn nhân vật và bản đồ |
| `Assets/Scenes/` | Scene gameplay và menu chính |

Các scene chính:

- **GigabonkMenu:** menu chính, chọn nhân vật, chọn map và Shop.
- **DesertArena:** đấu trường gameplay với địa hình sa mạc, vật cản và props.

## Yêu cầu

- Unity Editor `6000.3.10f1`
- Universal Render Pipeline (URP)
- Hệ điều hành và phần cứng hỗ trợ Unity 6

## Cài đặt và chạy dự án

1. Clone repository:

   ```bash
   git clone <repository-url>
   ```

2. Mở Unity Hub, chọn **Add project from disk** và trỏ tới thư mục vừa clone.
3. Mở dự án bằng Unity Editor `6000.3.10f1`.
4. Chờ Unity import toàn bộ asset và package.
5. Mở scene `GigabonkMenu` để bắt đầu từ menu hoặc `DesertArena` để kiểm tra gameplay.
6. Nhấn **Play** trong Unity Editor.

Có thể kiểm tra nhanh mã C# ngoài Unity bằng lệnh:

```bash
dotnet build Gigachad.sln
```

## Thành viên

**Nhóm HIT-Gigachad**

## Trạng thái dự án

Dự án đã hoàn thiện gameplay loop survival chính, hệ thống meta-progression, các loại vũ khí và tome, enemy horde, raid wave cùng nhiều boss. Các kỹ thuật Jobs, Burst, AI LOD, Mesh Flipbook, VAT và environment chunking được áp dụng để duy trì hiệu năng khi có số lượng lớn enemy trên màn hình.
