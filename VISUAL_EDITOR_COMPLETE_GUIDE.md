# Visual Live Editor - Complete Guide (Multi-Page Edition)

## 📌 Quick Summary

You now have a **visual live editor** that lets you edit **ALL pages** (Homepage, About, Services, Contact) and **ALL sections** with hover-and-click editing. No tables, no complex forms — just click on the page and edit.

## 🚀 Getting Started

### 1. Access the Editor
- Log in as **Admin**
- Go to **Admin Dashboard** → **Visual Live Editor**
- Direct URL: `/Admin/VisualEditor`

### 2. Switch Pages
Click the icons on the left sidebar:
- 🏠 **Homepage**
- ℹ️ **About Page**
- 🛠️ **Services Page**
- ✉️ **Contact Page**

### 3. Edit Any Section
1. **Hover** over a section → See blue highlight + label
2. **Click** the section → Editor panel opens on the right
3. **Edit** the content in the textarea
4. **Upload image** (if the section supports it)
5. **Click Save** → Changes appear instantly

## 🎨 Features

### ✅ Hover & Click Interface
- Hover over any section to highlight it
- Section name appears in a blue label
- Click to open the editor

### ✅ Live Preview
- See the actual page layout while editing
- Sections highlight as you hover
- Selected section shows a darker highlight

### ✅ Real-Time Updates
- Changes save via AJAX (no page reload)
- Preview updates instantly
- Success message confirms save

### ✅ Image Upload
- Upload images inline
- See current image preview
- Supports JPG, PNG, WebP (max 5MB)

### ✅ Multi-Page Support
- Edit all 4 pages from one interface
- Switch instantly with tab icons
- All 81 sections are editable

### ✅ HTML Support
- Use HTML tags in content:
  - `<strong>` and `<em>` for emphasis
  - `<h1>-<h6>` for headings
  - `<p>` for paragraphs
  - `<a>` for links
  - `<br>` for line breaks
  - And more!

### ✅ Responsive Design
- Works on desktop, tablet, mobile
- Editor panel adjusts on smaller screens
- Touch-friendly interface

## 📖 Step-by-Step Examples

### Example 1: Change Homepage Hero Title
```
1. Click 🏠 (Homepage tab)
2. Hover over the main "QONNEC" heading
3. See blue highlight with "Hero Title" label
4. Click it
5. Editor panel opens → Edit the HTML
6. Click "Save Changes"
7. Homepage updates with new title
```

### Example 2: Update About Page Mission
```
1. Click ℹ️ (About tab)
2. Find "Our Mission" section
3. Hover → Click it
4. Edit the mission statement
5. Save
6. About page shows new mission
```

### Example 3: Add Service Image
```
1. Click 🛠️ (Services tab)
2. Click a service section
3. Scroll down in editor panel
4. Click "Upload Image"
5. Select an image file
6. Click "Save Changes"
7. Image displays on services page
```

### Example 4: Edit Contact Information
```
1. Click ✉️ (Contact tab)
2. Find contact info section
3. Click to edit
4. Update phone number, email, address
5. Save
6. Contact page reflects changes
```

## 🎯 Editable Sections by Page

### Homepage (7 main sections)
- Hero Title
- Hero Description
- Feature 1
- Feature 2
- Feature 3
- Call to Action Title
- Call to Action Description

### About Page (14 sections)
- Hero Title & Description
- Our Story Title, Intro, Content
- Mission Title & Content
- Values Title, Value 1-3 (with titles & descriptions)

### Services Page (39 sections)
- All service categories and details
- Service descriptions
- Service features and benefits

### Contact Page (21 sections)
- Contact form fields
- Contact information
- Support details
- Map and location info

## 💡 Best Practices

### Content Guidelines
✅ Keep hero titles **short** (1-2 words)  
✅ Write descriptions **clear and concise** (1-3 sentences)  
✅ Use **simple HTML** (avoid complex nested tags)  
✅ **Optimize images** before uploading (max 5MB)  
✅ Use **web-friendly formats** (JPG, PNG, WebP)  

### HTML Tips
```html
<!-- ✅ Good -->
<h2>Our Mission</h2>
<p>We help teams <strong>collaborate</strong> and <em>innovate</em>.</p>
<a href="/about">Learn more</a>

<!-- ❌ Avoid -->
<p><h2>Bad nesting</h2></p>
<div onclick="alert('xss')">Click me</div>
```

### Image Recommendations
- Hero images: 1200x600px
- Service images: 400x300px
- Compress before upload
- Use consistent image sizes

## ⚙️ Technical Details

### API Endpoints
```
GET /Admin/VisualEditor              → Main page
GET /Admin/GetLivePagePreview?page=X → Page preview (home/about/services/contact)
GET /Admin/GetSectionData?id=X       → Section data
POST /Admin/SaveSectionData          → Save content & images
```

### Section ID Mapping
```
Homepage:
1 = Hero Title, 2 = Hero Body
3-8 = Features 1-3 (title + body)
6-7 = CTA (title + body)

About:
11-12 = Hero, 13-15 = Story
16-17 = Mission, 18 = Values title
19-24 = Values 1-3

Services: 
All service sections

Contact:
All contact sections
```

### Database
- All sections stored in `HomePageSections` table
- SectionKey field identifies section type
- Content stored as HTML
- ImagePath stores uploaded image location

## 🔒 Security Features

✅ **CSRF Protection** — All POST requests validated  
✅ **Admin Only** — Requires Admin role  
✅ **File Validation** — Images checked before upload  
✅ **Path Safety** — Files saved to controlled directory  
✅ **Database Safety** — Proper parameterized queries  

## 🆘 Troubleshooting

### Problem: Editor panel won't open
**Solution:** Try refreshing the page. Check browser console for errors (F12).

### Problem: Changes not saving
**Solution:** Check your internet connection. Ensure you're logged in as Admin. Look for error messages in the editor panel.

### Problem: Image won't upload
**Solution:** Check file size (max 5MB). Use supported format (JPG, PNG, WebP). Check file permissions on server.

### Problem: Preview looks wrong
**Solution:** Check HTML syntax (close all tags). Try reloading the page. Clear browser cache.

### Problem: Page tab not responding
**Solution:** Refresh the page. Check browser console for JavaScript errors.

## 📊 Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **Escape** | Close editor without saving |
| **Tab** | Navigate between form fields |
| **Ctrl+A** | Select all text in textarea |

## 🎓 Learning Resources

- **[VISUAL_EDITOR_QUICKSTART.md](VISUAL_EDITOR_QUICKSTART.md)** — 30-second quick start
- **[VISUAL_EDITOR_IMPLEMENTATION.md](VISUAL_EDITOR_IMPLEMENTATION.md)** — Technical deep dive
- **[VISUAL_EDITOR_MULTIPAGE.md](VISUAL_EDITOR_MULTIPAGE.md)** — Multi-page features

## 📞 Support

**Question?** Check the documentation files  
**Found a bug?** Report it with a screenshot and steps to reproduce  
**Need a feature?** Suggest it and we can add it  

## ✅ Checklist for First Use

- [ ] Log in as Admin
- [ ] Go to Admin Dashboard
- [ ] Click "Visual Live Editor"
- [ ] Click Homepage tab (🏠)
- [ ] Hover over a section and see it highlight
- [ ] Click it to open editor
- [ ] Make a small change
- [ ] Click "Save Changes"
- [ ] See preview update
- [ ] Try switching pages
- [ ] Edit a section on About page
- [ ] Experiment with HTML formatting
- [ ] Try uploading an image

## 🎉 You're Ready!

The visual editor is **fully functional** and **ready to use**. Start editing your pages visually — no more tables, no more complex forms, just point and click!

---

**Questions?** See the documentation.  
**Ready to start?** Go to `/Admin/VisualEditor` now!

**Version:** 2.0 (Multi-page)  
**Updated:** December 4, 2025  
**Status:** ✅ Production Ready
