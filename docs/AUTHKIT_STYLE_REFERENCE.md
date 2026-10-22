# AuthKit Style Reference Specification
> **Theme:** Dark — Frosted Glass Cathedral at Midnight  
> **Source Reference:** WorkOS / AuthKit Design System  
> **Archived For:** Quiz Management System (`PRN232_Group5` & `PRM393_Group1`)

---

## 1. Triết lý Thiết kế (Design Philosophy)

AuthKit đem lại cảm giác thẩm mỹ của một đêm ra mắt sản phẩm cao cấp (**Midnight Product-Launch Aesthetic**):
- **Midnight Canvas**: Nền đen huyền bí sâu thẳm (`#05060f`), mô phỏng không gian vũ trụ tĩnh lặng.
- **Frosted Glass Cathedral**: Các khối bề mặt tựa như những tấm kính mờ nhiều lớp xếp chồng lên vầng hào quang phát quang (ambient glow).
- **Luminous Typography**: Chữ phát sáng nhẹ nhàng như được chiếu đèn từ phía sau lớp kính, tạo cảm giác lộng lẫy và tinh tế.
- **Chỉ 1 Điểm Nhấn Sắc Màu Duy Nhất (Single Chromatic Accent)**: Sắc tím **Void Violet** (`#663af3`) độc quyền cho nút hành động chính (Submit / Continue CTA), tương phản tuyệt đối trên nền đen mà không phá vỡ vẻ trang nhã.
- **Glass Edge & Hairline**: Không dùng đường viền (border) cứng đặc. Mọi bề mặt tương tác sử dụng viền kẻ siêu mảnh mờ (inset hairline `rgba(186, 215, 247, 0.12)`).

---

## 2. Bảng Mã Màu & Design Tokens (Color Palette)

| Tên Màu (Token Name) | Giá Trị Hex / RGBA | Biến CSS Token | Vai Trò & Vị Trí Áp Dụng |
| :--- | :--- | :--- | :--- |
| **Midnight Canvas** | `#05060f` | `--color-midnight-canvas` | Nền trang sâu nhất, mặt đáy của card, nền badge |
| **Steel Plate** | `#2f343e` | `--color-steel-plate` | Mặt nổi (elevated), nền nút phụ (ghost/secondary button), panel |
| **Fog Veil** | `#9da7ba` | `--color-fog-veil` | Chữ phụ, văn bản giải thích mờ (helper copy), phân cấp sau tiêu đề |
| **Moon Mist** | `#c7d3ea` | `--color-moon-mist` | Chữ nội dung (body text), nhãn thứ cấp, placeholder mờ |
| **Frost Glow** | `#d1e4fa` | `--color-frost-glow` | Màu chữ chính, link, icon phát sáng — màu chữ mặc định |
| **Ice Highlight** | `linear-gradient(0deg, #d8ecf8 0%, #98c0ef 100%)` | `--color-ice-highlight` | Gradient chữ cho logo/tiêu đề lớn, độ tương phản cực đại |
| **Pure White** | `#ffffff` | `--color-pure-white` | Chữ trên nút CTA, text input khi gõ, điểm sáng cao nhất |
| **Void Violet** | `#663af3` | `--color-void-violet` | **Điểm nhấn duy nhất**: Nút CTA chính (Continue/Submit Form) |
| **Blueprint Blue** | `#b6d9fc` | `--color-blueprint-blue` | Điểm nhấn icon trang trí, ánh sáng phủ (soft highlight wash) |
| **Ember Glow** | `#e46d4c` | `--color-ember-glow` | Màu phụ cho palette swatch / thương hiệu mở rộng |
| **Signal Blue** | `#027dea` | `--color-signal-blue` | Màu phụ cho palette swatch / trạng thái liên lạc |
| **Deep Teal** | `#269684` | `--color-deep-teal` | Màu phụ cho palette swatch / độ hoàn thành |
| **Gridline Blue** | `#3f4959` | `--color-gridline-blue` | Màu bóng đổ ngoài có sắc thái xanh lạnh (tinted shadow) |
| **Glass Edge** | `rgba(186, 215, 247, 0.12)` | `--color-glass-edge` | Đường viền hairline kính mờ 1px inset cho inputs, cards, pills |
| **Luminous Fill** | `rgba(199, 211, 234, 0.12)` | `--color-luminous-fill` | Nền mờ cho badge, tag, vùng phủ UI mờ |

---

## 3. Hệ Thống Typography & Phông Chữ

### Phông Chữ Chính
- **Untitled Sans** *(Thay thế: `Inter`)*: Dùng cho Body, UI buttons, form inputs, badges, subheadings.
  - Weights: `400`, `500`, `600`, `700`
  - Sizes: `12px`, `14px`, `16px`, `18px`, `24px`
  - Letter-spacing: `-0.01em`
- **aeonikPro** *(Thay thế: `Space Grotesk`)*: Dùng riêng cho tiêu đề lớn (Display headings, Hero title).
  - Weights: `400`, `500`
  - Sizes: `28px`, `44px`, `48px`
- **dotDigital** *(Thay thế: `JetBrains Mono`)*: Dùng cho nhãn phụ phía trên tiêu đề (All-caps Eyebrow labels).
  - Weights: `400`, Size: `15px`, Tracking: `0.10em` (chữ hoa giãn cách).

### Thang Cỡ Chữ (Type Scale)

| Vai Trò | Cỡ Chữ | Chiều Cao Dòng (Leading) | Giãn Cách (Tracking) | CSS Variable |
| :--- | :--- | :--- | :--- | :--- |
| **caption** | `12px` | 1.33 | `0px` | `--text-caption` |
| **body-sm** | `14px` | 1.43 | `0px` | `--text-body-sm` |
| **body** | `16px` | 1.50 | `-0.16px` | `--text-body` |
| **subheading** | `18px` | 1.33 | `0px` | `--text-subheading` |
| **heading-sm** | `24px` | 1.17 | `-0.24px` | `--text-heading-sm` |
| **heading** | `28px` | 1.14 | `0px` | `--text-heading` |
| **heading-lg** | `44px` | 1.16 | `0px` | `--text-heading-lg` |
| **display** | `48px` | 1.17 | `0px` | `--text-display` |

---

## 4. Khoảng Cách (Spacing) & Bán Kính Bo Góc (Border Radius)

- **Base Unit**: `4px`
- **Thang đo khoảng cách**:
  `4px`, `8px`, `12px`, `16px`, `20px`, `24px`, `32px`, `36px`, `40px`, `48px`, `56px`, `100px`, `120px`, `200px`.
- **Quy tắc Bán Kính (Strict Radius Rules)**:
  - **Cards & Modals**: `16px` (`--radius-cards`)
  - **Inputs & Badges**: `6px` (`--radius-inputs`)
  - **Buttons & Tags**: `999px` (Pill shape tuyệt đối)
  - **Icon Containers**: `9999px` (Hình tròn hoàn hảo)

---

## 5. Hiệu Ứng Bề Mặt & Đổ Bóng (Surfaces & Glass Elevation)

Không dùng bóng đổ đen đơn điệu (`box-shadow: 0 4px 6px #000`), mà kết hợp đa tầng ánh sáng kính mờ:
- **Auth-Form Modal Card**:
  ```css
  box-shadow:
    inset 0 1px 1px rgba(216, 236, 248, 0.20),
    inset 0 24px 48px rgba(168, 216, 245, 0.06),
    0 16px 32px rgba(0, 0, 0, 0.30);
  background: rgba(5, 6, 15, 0.97);
  backdrop-filter: blur(20px);
  border-radius: 16px;
  ```
- **Feature Glass Card**:
  ```css
  box-shadow:
    inset 0 1px 1px rgba(199, 211, 234, 0.12),
    inset 0 24px 48px rgba(199, 211, 234, 0.05),
    0 24px 32px rgba(6, 6, 14, 0.70);
  background: rgba(186, 214, 247, 0.03);
  backdrop-filter: blur(16px);
  border-radius: 16px;
  ```

---

## 6. Bộ Mã Nguồn Mẫu (Quick Reference Implementation)

### 6.1. CSS Custom Properties (`:root`)
```css
:root {
  /* Colors */
  --color-midnight-canvas: #05060f;
  --color-steel-plate: #2f343e;
  --color-fog-veil: #9da7ba;
  --color-moon-mist: #c7d3ea;
  --color-frost-glow: #d1e4fa;
  --color-ice-highlight: #d8ecf8;
  --gradient-ice-highlight: linear-gradient(0deg, #d8ecf8 0%, #98c0ef 100%);
  --color-pure-white: #ffffff;
  --color-void-violet: #663af3;
  --color-blueprint-blue: #b6d9fc;
  --color-ember-glow: #e46d4c;
  --color-signal-blue: #027dea;
  --color-deep-teal: #269684;
  --color-gridline-blue: #3f4959;
  --color-glass-edge: rgba(186, 215, 247, 0.12);
  --color-luminous-fill: rgba(199, 211, 234, 0.12);

  /* Typography */
  --font-untitled-sans: 'Inter', system-ui, -apple-system, sans-serif;
  --font-aeonikpro: 'Space Grotesk', system-ui, -apple-system, sans-serif;
  --font-dotdigital: 'JetBrains Mono', monospace;

  /* Elevation */
  --shadow-glass-card: inset 0 1px 1px rgba(199, 211, 234, 0.12),
                       inset 0 24px 48px rgba(199, 211, 234, 0.05),
                       0 24px 32px rgba(6, 6, 14, 0.7);
  --shadow-glass-modal: inset 0 1px 1px rgba(216, 236, 248, 0.20),
                        inset 0 24px 48px rgba(168, 216, 245, 0.06),
                        0 16px 32px rgba(0, 0, 0, 0.30);
}
```

### 6.2. Tailwind v4 Config Snippet
```css
@theme {
  --color-midnight-canvas: #05060f;
  --color-steel-plate: #2f343e;
  --color-fog-veil: #9da7ba;
  --color-moon-mist: #c7d3ea;
  --color-frost-glow: #d1e4fa;
  --color-ice-highlight: #d8ecf8;
  --color-pure-white: #ffffff;
  --color-void-violet: #663af3;
  --color-blueprint-blue: #b6d9fc;
  --color-glass-edge: rgba(186, 215, 247, 0.12);

  --font-sans: 'Inter', system-ui, sans-serif;
  --font-display: 'Space Grotesk', system-ui, sans-serif;
  --font-mono: 'JetBrains Mono', monospace;

  --radius-card: 16px;
  --radius-input: 6px;
  --radius-button: 999px;
}
```
