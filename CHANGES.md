# v1.3.3
Important bug-fix for the auto-updater so it receives new versions correctly. If you are currently on v1.3.2, you will not receive automatic updates because of it. Sincerest apologies for the inconvenience.

## Fixes
### v1.3.3
- Fixed a *bit* of an oversight with the updater changes.
### v1.3.2
- Added preliminary support for arbitrary parameter space in the UI.
- Default controller templates and animations are now included in II.
- Fixed a UI issue with dark mode that caused text to appear black.
- Revised the auto-updater to support future versions.
### v1.3.1
- Bandaged an issue with Menu creation that caused incorrect Menus to be found or used if their names were similar and contained whitespace. (Found by @MikeMatrix)
### v1.3.0
- **All** instances of the chosen Animator are now replaced in the Avatar Descriptor.
- Fixed a long-standing bug with new Page creation that occurred when names were already in use (The correct item should now be renamed).
- Fixed a bug with the "Corrupt Member" error message that didn't specify the correct item when triggered by buttons. (Found by @MikeMatrix)
- Aaaaaand here's all the Unity 2019 related ones:
	- Changed references for "UnityEngine.Experimental.UIElements" to "UnityEngine.UIElements" so it would compile.
	- Removed the IsDirtyUtility Subclass since the "EditorUtility.IsDirty" function is Public in Unity 2019.
	- Added a bar to the tops of Reorderable list footers since they no longer have one by default.
	- Fixed Data & Memory usage bar colors so that they work in both light & dark mode.
	- Fixed the styling of ToolbarTextFields to not blend in with the color of Box sections.
	- Changed the style of overlapping Boxes to Helpboxes since the border of Boxes was removed.
	- Adjusted some spacing between elements in the Manager window to prevent the Create button from going off the screen.
	- Subtly changed the way the destination option was displayed in the manager to better align with other settings.
	- Adjusted the About window to account for the new VERSION file format.
	- Edited the text in the About window as well as several Textfields & Labels scattered in other areas to support dark theme.
	- Edited the "Append Preset" window to both support dark theme and to not crunch the "No Preset Selected" text.
	- Corrected some code involving asset loading that caused submenus to not be preserved during export in 2019 exclusively.
----------------

*(The major version's release notes will now follow.)*

----------------
WOOT UNITY 2019 IS HERE BABY! DARK MODE FOR ALL! *cough*- sorry about that. *Anyways*, this is a **feature update**! Several new features have been added, as well as support and compatibility for Unity 2019.

Due to the somewhat sudden release of VRChat's Unity 2019 upgrade, this version hasn't been able to be vigorously tested in the updated engine. **Visual bugs in the Editor may be present, if you find one, please report it to the Issue's page on GitHub, not to me on Discord.**

## New Features
- **Unity 2019 Support!**
	Following VRChat in its footsteps, Inventory Inventor is now being built upon Unity 2019. All UI elements have been adjusted to work with the new theming and design language better.
- **Dark Mode Compatibility**
	With the upgrade to Unity 2019, everyone now has free access to Dark mode. Because of this, support for it has now been implemented into Inventory Inventor.
- **Copying & Pasting**
	Copying and pasting is now possible with Pages, Items, & Groups. Both options can be found when right-clicking them in the Directory (or the header for Groups). 
	- Settings are copied to the system buffer, so any changes made after copying settings won't be pasted. It will also overwrite whatever you currently have saved to the clipboard.
	- All members copied from one type of Group can be pasted as any other type, meaning you can copy a Button's Group and paste it into a Toggle's Enable or Disable Group for example and vice-versa. (Suggested by @noideaman)
- **More Context Menu Options**
	Right-clicking Pages, Items, & Groups now provides several more options than before:
	- In addition to copying & pasting, you can now duplicate or delete Pages & Items after right-clicking them in the Directory.
	- You can now set all members of a Group at once or clear a Group entirely after right-clicking the Group header.
- **Additional Toggle Settings**
	When using Animation Clips for Toggles, new options have been added for better control and adaptability:
	- You can now set a duration for the transition in either fixed or normalized time, this is useful for blending animations such as those that affect opacity or blend shapes.
	- You can now offset the animation's starting point while your avatar is loading, this is useful for progressive animations such as dissolves.

## Changes
- **General**
	- The location of relevant windows such as the Manager have been relocated to "Tools/Joshuarox100" instead of "Tools/Avatars 3.0"
	- The README has been updated with new documentation for introduced features and includes some modifications to the Troubleshooting section.
	- A new social preview for the GitHub has been applied and is also included in the "Images" folder.
- **Inventory Manager**
	- The last used destination is now saved within each Preset and is automatically used if possible when selecting the Preset in the Manager. (Suggested by @sinni800)
- **Presets**
	- Toggles with syncing disabled (local-only) are now able to be saved locally.
	- The icon for Presets has been updated to match the new design language of the Editor.