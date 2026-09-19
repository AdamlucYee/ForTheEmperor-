# FOR THE EMPEROR · Desktop Companion

v2.2 — English and Simplified Chinese. [Download the Windows x64 portable ZIP](release/ForTheEmperor-v2.2-win-x64.zip).

An unofficial Warhammer 40,000-themed Windows desktop companion with eight chapter appearances, animated attacks and a user-triggered file recycling command.

## Start

Run `ForTheEmperor.exe` on Windows 10/11 x64. The executable includes the .NET desktop runtime and character sprites; no SDK or separate .NET installation is required. First launch may take a little longer while native runtime files are extracted to your user temporary directory.

Choose **English** in the first-launch language window. Your choice is saved. You can switch between English and Simplified Chinese at any time using **Language** in the command panel. Existing chapter and size settings are preserved when upgrading.

## Controls

- Drag the marine to move it.
- Click for a chapter quote; double-click to open the command panel.
- Right-click the marine to change chapters, run a demo, recall or exit.
- Try **Run execution demo** first. Demo mode never deletes real files.
- Closing the command panel leaves the marine running. Use **Exit and remove file command** in the pet or tray menu to exit.

## File execution

With **Enable file execution command** selected, right-click a regular file directly on your desktop. On Windows 11, select **Show more options**, then **Execute in the Emperor's name**.

The marine moves to the target and attacks before the file is moved to the Windows Recycle Bin. Press **Ctrl + Alt + Esc** before impact to cancel; you can also cancel from the command panel or pet menu. After a successful operation, restore the file using the Windows Recycle Bin if needed.

The app handles one file at a time. It rejects folders, network paths, reparse points and files in desktop subfolders, and checks the target's identity again before recycling. There is no permanent-delete fallback. Deleting a shortcut only recycles the shortcut.

## Settings and language

Preferences and local logs are stored in `%LOCALAPPDATA%\ForTheEmperor\`. Logs may contain the names of processed files. The app uses the current-user registry for its temporary Explorer command. It does not require administrator rights, configure auto-start or contact an online AI service.

Application text is localized; Windows-provided dialogs and operating-system error details may follow your Windows language. Character quotes are presented as text, with optional Windows system sounds, not voice acting.

For a demo without saving settings or changing Explorer integration, launch:

```powershell
.\ForTheEmperor.exe --preview --language en
```

## Artwork and references

The eight character sprite atlases were generated with ImageGen from text prompts describing Warhammer chapter designs. Visual-direction research included [Ignacio Polo Garzón's Space marine](https://ignaciopolo.artstation.com/projects/V2LN2n) and [DaTico's pixel artwork](https://www.newgrounds.com/art/view/datico/in-the-grim-darkness-of-the-far-future-there-is-only-pixel-art).

The final generation calls did not use those artists' images as image inputs, and their original images were not bundled with the app. The sprites are AI-generated, not hand-drawn original Warhammer character designs. Runtime animation and particle effects are implemented in the application. Full prompts are recorded in `assets/sprites/generation-prompts.json` in the source repository.

This project is unofficial and is not affiliated with or licensed by Games Workshop. Warhammer-related intellectual property and referenced artwork remain subject to their respective owners' rights. Attribution and AI generation do not constitute commercial permission or an endorsement. No official approval is claimed.
