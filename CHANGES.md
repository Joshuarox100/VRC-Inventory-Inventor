# v1.2.0
I should probably be focusing on my college work instead of this...

This is a **FEATURE RELEASE**.  
Several things have changed with this release, but all your existing Presets will be usable **after following the steps in [Update Instructions](#update-instructions)**.  

*Due to the amount that has either been added or changed in this release, you may encounter some minor bugs that I failed to expect or encounter during testing. If you encounter something during usage that you believe is either a bug or unintentional, please contact me on Discord or leave a post under the Issues tab here in GitHub. You can find links to both of these resources in the README.*

## Summary
Summarizing this release is a tad difficult due to how much has changed. 

There have been many additions and changes relating to Presets such as the ability to **import Expressions Menus, copying Pages from other Presets, and moving Items between Pages**. Additionally, there are **several tweaks to the tool as a whole** as well as the addition of an **automatic installer for future updates**. There are also a couple **fixes to a few critical flaws** that I was not made aware of until recently.

## Update Instructions
**To update to this release from a previous version, please delete the version you previously had first. This should include the following folders/files: "Assets/Inventory Inventor" and "Assets/Gizmos/InventoryPreset Icon.png". If you do not delete these files first, you will have compiler errors and missing icons for Presets.**

*Additionally, although it is not required, it's recommended that you enter the settings and manually upgrade all your Presets after updating to this release, since the code that does it automatically will only run on future updates installed through the tool. If you do not do this, your Presets will not have the correct icon and they will also not be usable in various menus. Selecting a Preset in the Project window will also automatically upgrade it and allow it to be used again, but the button in the settings will be quicker if you have several Presets in the same Project.*

## New Features
- **Importing External Expressions Menus**
	This is a new way to create Pages within a Preset. If you already have a VRC Expressions Menu that you want to have somewhere inside the Preset, you can import it as a new Page using this option. This option can be found by clicking on the icon to the right of the 'Create Page' button.
	- If you choose to, you can also import any submenus within the menu's structure as well. This will import every submenu found in the menu, *as well as those within those submenus.* With this option, all Controls referring to imported submenus will be remade as Subpages instead.
- **Appending Other Presets**
	This is another new way to create Pages within a Preset. You can use this option to copy Pages from another Preset. After selecting a Preset to copy from, all its Pages will be listed and you can choose which Pages you want to add. This option can also be found by clicking on the icon to the right of the 'Create Page' button.
	- If you copy a Page containing a Subpage Item without copying the Page it refers to, it will not lead anywhere.
	- If you copy a Page containing a Toggle that has Group that refers to an Item on a Page that was not copied, it will be set to none.
	- If you copy a Page with a name that's already used in the Preset you're copying to, it will be renamed as it normally would.
- **Moving Items between Pages**
	You are now able to move Items between Pages by right-clicking on them and choosing a different Page.
	- Pages that already contain the maximum number of items are excluded from the selectable list. If no Pages are able to be used, right-clicking an Item will simply not do anything.
- **Project Settings Support**
	Inventory Inventor now has a few persistent settings you can access within Project Settings ('Edit -> Project Settings').
	- You can enable or disable the Automatic Update Checker (see below).
	- You can choose to allow usage of animations that modify invalid types of properties (see below).
	- You can change the default path the Manager reverts to when it is unable to use the one you assigned (Must be located within the Assets folder).
	- You can choose to automatically search for and upgrade any older Presets found within the Project.
- **Automatic Update Installer**
	If enabled in the settings, Inventory Inventor will silently check for any updates when Unity is starting and will prompt you to install them. This is also implemented in the 'Check for Updates' button.
	- If you choose to install them, they will automatically be downloaded and properly imported.<br>*(All Presets in your Project will automatically be upgraded if needed when a new update is installed.)*
	- If no updates were found for any given reason (lack of Internet, already using the latest, etc.), nothing will be displayed.
- **Allowing Invalid Animations**
	The new settings have an additional setting for allowing animations that modify invalid types .
	- All animations used in Presets are applied in the FX layer. This setting allows you to use Animations that normally wouldn't work correctly in this layer. There are several cases where this behavior does work despite VRChat's warnings, so this option has been included for those who know what they're doing and want to bypass the type restriction.
- **New Example Assets**
	There are now a few more example assets for you to refer to included in the package.
	- The existing example has been named 'Complex Cubes'.
	- A new example called 'Simple Shapes' is a more updated example using some of the newer features included in v1.1.x.
	- One of the Presets, 'Tutorial', featured in the README's screenshots is now included.

## Changes
- **Overall**
	- Swapped the placement of Yes/No options to be consistent with the rest of the tool and Unity as a whole (sorry for ruining your muscle memory).
	- Code has been further divided into separate files to improve readability.
- **File Structure**
	- The overall folder structure has been expanded to improve file organization.
	- All scripts relating to Inventory Inventor are now (if possible) integrated into a unique namespace, preventing any possible naming conflictions with other tools.
	- The 'Example' folder has been renamed to 'Examples' and contains additional examples.
	- BMB Libraries is now stored internally under 'Libraries' instead of in its own folder at the Project root.
- **Inventory Manager**
	- The last destination used for applying a Preset is saved and used the next time the Manager is opened.
	- The window for removing Inventories now highlights Expression Parameters in purple when the corresponding option to remove them is selected.
	- The window for removing Inventories now has a sticky header in the scrollable portion.
	- 'Check for Updates' has been modified to utilize the Automatic Update Installer.
- **Presets**
	- Made Pages easier to select and reorder.
	- Pages can now contain zero Items.
	- The Item a Group entry refers to can now be set to none.
	- Subpages can now lead nowhere.
	- Buttons now are actually controlled by buttons ingame.
	- A Control's type is now labelled as a subtype to reduce confusion.
	
## Fixes
- Added a null check for using Controls on an avatar without assigned parameters.
- Fixed an ingame syncing issue that was caused by some VRChat update regarding buttons.