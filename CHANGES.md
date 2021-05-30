# v1.2.3
Normally, I'd wait to implement more changes before releasing a patch or update, but just from my own personal observations in VRChat, this feels like an immediate necessity. So you know how in the last patch, I talked about that problem with saved Expression Parameters that wasn't my fault and I wasn't going to fix?  
**Yeah, I made a fix for it anyway...**  

**After installing this patch, you will need to reapply your presets to your avatars to see the fix.**

## Fixes
### v1.2.3
- Worked around the saved parameter issue to display the default state of the inventory to remote clients until the local client has loaded their avatar themselves.
### v1.2.2
- Added several null checks to fix problems when viewing presets for an avatar without Expression Parameters assigned.
- Fixed an oversight that caused the script to use the wrong animation file generated for objects.
### v1.2.1
- Added a null check for Subpage Items when they can't find the page they link to during appending.
### v1.2.0
- Added a null check for using Controls on an avatar without assigned parameters.
- Fixed an ingame syncing issue that was caused by some VRChat update regarding buttons.