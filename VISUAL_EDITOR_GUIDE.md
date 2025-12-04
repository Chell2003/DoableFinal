# Visual Live Editor - CMS Guide

## Overview
The Visual Live Editor transforms your CMS into an interactive, real-time editing experience. Instead of navigating through tables and forms, you now see the actual landing page and click directly on any section to edit it instantly.

## Features
✅ **Live Preview** — See the actual page layout and content in real-time  
✅ **Hover & Click** — Hover over any section to highlight it, then click to edit  
✅ **Instant Updates** — Changes save and display immediately without page reloads  
✅ **Image Upload** — Upload hero images, service images, and more inline  
✅ **Clean Interface** — No tables or confusing navigation — pure visual editing  
✅ **Responsive Design** — Works on desktop and tablet displays  

## How to Access
1. Log in as **Admin**
2. Go to **Admin Dashboard**
3. Click **Visual Live Editor** (in the Content Management System section)

## Using the Visual Editor

### Editing a Section
1. **Hover** over any section on the page — it will highlight with a blue border
2. A label appears showing the section name (e.g., "Hero Title", "Feature 1")
3. **Click** the section to open the editor panel on the right
4. **Edit** the content directly in the textarea
5. (Optional) **Upload** a new image if the section supports it
6. **Click "Save Changes"** to update the page instantly

### Editable Sections
- **Hero Title** — Main headline on the page
- **Hero Description** — Subtitle and CTA buttons area
- **Feature 1, 2, 3** — Feature cards with icon and description
- **Call to Action** — Main CTA section heading and body text

### Supported Content
- **Plain text** — Simple text content
- **HTML** — You can use HTML tags:
  - `<strong>` and `<em>` for emphasis
  - `<p>`, `<h1>` through `<h6>` for headings
  - `<a>` for links
  - `<br>` for line breaks

Example:
```html
<h3>Our Mission</h3>
<p>We help teams <strong>collaborate</strong> and <em>innovate</em> together.</p>
<a href="/about">Learn more</a>
```

### Uploading Images
1. Click on a section that supports images (e.g., Hero section)
2. Scroll down to the "Upload Image" section in the editor panel
3. Click to select an image file (JPG, PNG, WebP, max 5MB)
4. The image will be uploaded and displayed when you save

### Keyboard Shortcuts
- **Escape** — Close the editor panel without saving
- **Tab** — Navigate between form fields in the editor

### Saving & Publishing
- Changes are saved to the database **immediately** when you click "Save Changes"
- The live preview refreshes automatically
- No need to publish or deploy changes separately

## Tips & Best Practices

### Content Length
- **Hero Title** — Keep it short (1-2 words recommended)
- **Descriptions** — 1-3 sentences for best visual appearance
- **Feature Titles** — Short and punchy (3-5 words)

### HTML Usage
- Keep HTML simple and clean
- Test your changes in the preview before saving
- Links should be relative (e.g., `/about`) or absolute (e.g., `https://example.com`)

### Images
- Recommended dimensions: 1200x600px for hero images
- Optimize images before uploading to ensure fast loading
- Use web-friendly formats: JPG or PNG

### Undo/Redo
- There's no built-in undo — consider copying content before making major changes
- You can always revert to previous versions by editing again

## Troubleshooting

### Changes Not Saving
- Check your internet connection
- Ensure you're logged in as an Admin
- Look for error messages in the editor panel

### Images Not Displaying
- Ensure the file size is under 5MB
- Use supported formats: JPG, PNG, WebP
- Check file permissions in the uploads folder

### Preview Not Updating
- Try refreshing the page (F5)
- Check browser console for JavaScript errors (F12 > Console)
- Clear browser cache and reload

## Advanced Usage

### Resetting to Defaults
- If you want to restore default content, contact your administrator
- Default values are hardcoded in the application

### Bulk Edits
- You can still use the traditional CMS editors for editing multiple sections at once
- Links to traditional editors are available in the Admin Dashboard

## Performance Notes
- The visual editor loads the page in a live preview area
- Edits are validated on the client side before sending to the server
- Image uploads are optimized for performance

## Version History
- **v1.0** (Dec 4, 2025) — Initial release with hero, features, and CTA sections
- Future updates will include more page sections and editing options

---

**Questions?** Contact your site administrator for support.
