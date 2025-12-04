# Quick Start: Visual Live Editor

## 🚀 Launch in 30 Seconds

### 1. Log In
- URL: `https://localhost:7xxx` (or your app URL)
- Username: Admin account
- Password: Your admin password

### 2. Go to Admin Dashboard
- Click **Admin** in navigation
- Or direct URL: `/Admin`

### 3. Click "Visual Live Editor"
- Look for the blue button in the "Content Management System" section
- Direct URL: `/Admin/VisualEditor`

### 4. Hover & Edit
- **Hover** over any section → blue highlight appears
- **Click** the section → editor panel opens on the right
- **Edit** content in the text area
- **Save** → live preview updates instantly

## 🎯 Common Tasks

### Edit Hero Title
1. Click on the main "QONNEC" heading
2. Edit the HTML in the textarea
3. Save → Done!

### Change Feature Cards
1. Hover over a feature card (Task Management, Team Collaboration, Progress Tracking)
2. Click it
3. Edit the title and description
4. Save

### Add/Change Hero Image
1. Click the Hero section
2. Scroll down in editor panel
3. Click "Upload Image" and select a file
4. Save → image displays on the page

### Edit Call to Action
1. Click "Ready to Get Started?" section
2. Edit the text
3. Save

## 📝 Tips

✅ **HTML Support** — Use `<strong>`, `<em>`, `<a>`, `<h1>-<h6>`, etc.  
✅ **Preview Updates** — Changes appear instantly without page reload  
✅ **Multiple Edits** — Edit as many sections as you want in one session  
✅ **Image Upload** — Supports JPG, PNG, WebP (max 5MB)  
✅ **Escape Key** — Press Esc to close editor without saving  

## ⚡ What's Different

| Old Way | New Way |
|---------|---------|
| Admin → Manage Homepage → Click Edit → Modal opens | Admin → Visual Editor → Click section → Edit in place |
| Preview in separate tab | Preview always visible |
| Upload image → refresh → see result | Upload → instantly see result |
| Confusing form fields | See actual page layout |

## 🎨 Editable Sections

| Section | What to Edit |
|---------|-------------|
| Hero Title | Main headline (QONNEC) |
| Hero Description | Subtitle + image + buttons |
| Feature 1, 2, 3 | Title + description for each card |
| Call to Action | Main CTA heading + description |

## ⚙️ Technical Details

**URLs:**
- `/Admin/VisualEditor` — Main page
- `/Admin/GetLivePreview` — Fetches page HTML
- `/Admin/GetSectionData` — Fetches section details
- `/Admin/SaveSectionData` — Saves changes

**Sections Map:**
```
ID=1: Hero Title          (hero-title)
ID=2: Hero Description    (hero-body)
ID=3: Feature 1          (feature-1-title)
ID=4: Feature 2          (feature-2-title)
ID=5: Feature 3          (feature-3-title)
ID=6: CTA Title          (cta-title)
ID=7: CTA Description    (cta-body)
```

## 🆘 Troubleshooting

**"Section not found" error**
- Ensure you're editing the correct section
- Refresh the page and try again

**Image won't upload**
- Check file size (max 5MB)
- Use supported format: JPG, PNG, WebP
- Check browser console for errors (F12)

**Changes not saving**
- Ensure internet connection is active
- Check that you're logged in as Admin
- Try clearing browser cache and refreshing

**Preview looks wrong**
- Check HTML syntax in textarea
- Close/reopen editor
- Refresh the page

---

**Need help?** See [VISUAL_EDITOR_GUIDE.md](VISUAL_EDITOR_GUIDE.md) for detailed documentation.

**Found a bug?** Report it to your developer with a screenshot and steps to reproduce.
