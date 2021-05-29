# v1.2.2
This patch resolves some issues regarding the lack of parameters on avatars and animation generation.
On a side note, there is currently a problem with VRChat that causes remote clients to view your avatar with all saved parameters disabled if they load your avatar before you do yourself. The only real counter to this issue (until they fix it) is to either disable your avatar until you load it locally with a saved parameter or to not use saved parameters entirely. **This is not an issue with Inventory Inventor, stop asking me about it.**

## Fixes
### v1.2.2
- Added several null checks to fix problems when viewing presets for an avatar without Expression Parameters assigned.
- Fixed an oversight that caused the script to use the wrong animation file generated for objects.
### v1.2.1
- Added a null check for Subpage Items when they can't find the page they link to during appending.
### v1.2.0
- Added a null check for using Controls on an avatar without assigned parameters.
- Fixed an ingame syncing issue that was caused by some VRChat update regarding buttons.