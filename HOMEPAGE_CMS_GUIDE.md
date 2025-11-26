# Homepage CMS Setup Guide

## Overview
The homepage now has a Content Management System (CMS) that allows admins to edit all text content without changing the layout or design.

## How It Works

### Database Model
A new `HomePageSection` table stores all editable content:
- **SectionKey**: Unique identifier (e.g., "hero-title", "feature-1-title")
- **DisplayName**: Human-readable name shown to admins
- **Content**: HTML content that can be edited
- **CreatedAt/UpdatedAt**: Timestamps for tracking changes
- **UpdatedBy**: Which admin made the last change

### Editable Sections

The following sections are editable on the homepage:

#### Hero Section
- `hero-title` - Main heading (default: "QONNEC")
- `hero-body` - Hero paragraph with mission statement

#### Features Section
- `feature-1-title` - Task Management heading
- `feature-1-body` - Task Management description
- `feature-2-title` - Team Collaboration heading
- `feature-2-body` - Team Collaboration description
- `feature-3-title` - Progress Tracking heading
- `feature-3-body` - Progress Tracking description

#### CTA Section
- `cta-title` - Call-to-Action heading (default: "Ready to Get Started?")
- `cta-body` - CTA paragraph

## How to Use

### For Admins

1. **Log in as Admin**
   - Navigate to the homepage
   - You'll see an "Edit Homepage Content" button at the top

2. **Edit a Section**
   - Click the "Edit Homepage Content" button
   - Click "Edit" on any section you want to modify
   - Edit the HTML content in the text area
   - Preview the changes below
   - Click "Save Changes"

3. **Supported HTML Tags**
   - `<h1>`, `<h2>`, `<h3>`, `<h4>`, `<h5>`, `<h6>` - Headings
   - `<strong>`, `<b>` - Bold text
   - `<em>`, `<i>` - Italic text
   - `<p>` - Paragraphs
   - `<a href="#">` - Links
   - `<br>` - Line breaks
   - `<ul>`, `<ol>`, `<li>` - Lists
   - Any other standard HTML

### For Developers

#### Key Files

1. **Model**: `Models/HomePageSection.cs`
   - Defines the database schema
   - Properties: Id, SectionKey, DisplayName, Content, IconClass, SectionOrder, CreatedAt, UpdatedAt, UpdatedBy

2. **Service**: `Services/HomePageService.cs`
   - `GetAllSectionsAsync()` - Get all sections
   - `GetSectionByKeyAsync(key)` - Get specific section
   - `UpdateSectionAsync(id, content, userId)` - Update section content
   - `CreateSectionAsync(...)` - Create new section

3. **Controller**: `Controllers/AdminController.cs`
   - `ManageHomePage()` - List all sections
   - `EditHomePageSection(id)` - Edit form GET
   - `EditHomePageSection(id, content)` - Save changes POST

4. **Views**:
   - `Views/Admin/ManageHomePage.cshtml` - List sections
   - `Views/Admin/EditHomePageSection.cshtml` - Edit form with preview
   - `Views/Home/Index.cshtml` - Homepage (uses sections from database)

#### Database Setup

The migration `AddHomePageSections` creates the table automatically. To add new sections:

```csharp
var section = await homePageService.CreateSectionAsync(
    sectionKey: "my-section",
    displayName: "My Section Title",
    content: "<p>Default content</p>",
    order: 1
);
```

## Adding New Editable Sections

To add a new editable section to the homepage:

1. **Add section variable in Index.cshtml**:
```csharp
var myNewSection = sections.FirstOrDefault(s => s.SectionKey == "my-section")?.Content 
    ?? "<p>Default content</p>";
```

2. **Use it in the view**:
```html
@Html.Raw(myNewSection)
```

3. **Initialize in database** (first time accessed or seed data):
```csharp
await homePageService.CreateSectionAsync(
    "my-section",
    "My New Section",
    "<p>Default content</p>"
);
```

## Fallback Behavior

If a section doesn't exist in the database, the view uses a default value (set with the `??` operator). This ensures the homepage always displays correctly, even if sections haven't been created yet.

## Security

- Only users with the `Admin` role can:
  - View the "Edit Homepage Content" button
  - Access the manage/edit pages
  - Update section content

- The admin panel is protected with `[Authorize(Roles = "Admin")]`

## Best Practices

1. **Keep content concise** - Large amounts of HTML may slow down page loads
2. **Test HTML** - Always preview before saving to ensure formatting looks correct
3. **Use semantic HTML** - Prefer `<strong>` over `<b>`, `<em>` over `<i>`
4. **Avoid inline styles** - Use the existing CSS classes (bootstrap classes work)
5. **Links** - Use relative paths like `/path/to/page`

## Troubleshooting

### Sections not showing up
- Check if the HomePageService is registered in Program.cs
- Verify the database migration ran successfully
- Check browser console for errors

### HTML rendering as text
- Ensure you're using `@Html.Raw()` and not just `@` in the view
- Check that the section exists in the database

### Can't see Edit button
- Make sure you're logged in as an Admin
- Check that your user has the Admin role
