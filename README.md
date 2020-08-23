Inventory Inventor
==============

Author: Joshuarox100

Description: Make inventories fast with Inventory Inventor! With it, you can create inventories with up to 64 synced toggles, all contained within a single Expression Parameter!

Dependencies: 
- [BMB Libraries](https://github.com/Joshuarox100/BMB-Libraries) (Included)
- VRCSDK3-AVATAR (Not Included)

Setting Up
--------------
Before following these steps, set up your avatar how you normally would and ensure that you have a basic understanding of how Avatars 3.0 works.

1) Download and import the latest **Unity Package** from [**Releases**](https://github.com/Joshuarox100/VRC-Inventory-Inventor/releases) on GitHub **(You will have issues if you don't)**.

<p align="center">
  <img width="80%" height="80%" src="Images/Step 1.png">
</p>

2) Open the setup window located under Tools -> Avatars 3.0 -> Inventory Inventor.

<p align="center">
  <img width="80%" height="80%" src="Images/Step 2.png">
</p>

3) Next, configure how you want the inventory to be setup for your avatar.

<p align="center">
  <img width="80%" height="80%" src="Images/Step 3.png">
</p>

4) Finally, click Create to generate the inventory for your avatar! 
	>The inventory will be added to or replaced in the FX Animator contained within your descriptor. If an Animator hasn't been used for the FX layer, it will clone the VRCSDK's FX template and add it there. 

Everything should now be fully set up! If you have any issues or questions, look in the [troubleshooting](#troubleshooting) and [questions](#common-questions) section below before [contacting me](#contacting-me).

Common Questions
--------------
**Can I make menus within menus using the UI?**
>Not yet! That feature is planned for the future though.

**Can I have multiple inventories on a single avatar?**
>Let me answer your question with a another question: *Why do you need more than 64 toggles to begin with?*  
	Truthfully though, if you are seriously needing that much inventory space, perhaps you should consider splitting it up into multiple avatars for performance reasons alone or consider other ways to achieve what you're attempting to do. That said, once I do implement submenu creation in my UI, I will be raising the limit to 85 items to accomodate it.

**How do people who join the world late see me?**
>If you leave Auto Sync on, the current state of your inventory will be synced to them over a short period of time while the system is idle. If Auto Sync is left off, late-joiners will only see the initial state of the objects until you toggle them again, a bit like how toggles work in Avatars 2.0.

Troubleshooting
--------------
**My Inventory isn't syncing correctly to people joining late**
>Your Refresh Rate may be too fast for the network to handle. Try recreating your inventory using a slower time.

**The Debug menu is just showing random numbers for each of the item layers.**
>This is a visual bug caused by having State Machines named differently than their originating Layer. This doesn't actually cause any problems remotely or locally so you don't need to worry about it too much. A bug report for it exists on the Feedback forum if you want to upvote it [here](https://feedback.vrchat.com/avatar-30/p/bug-debug-menu-fails-to-show-state-names-when-the-state-machine-is-named-differe).

**"An exception occured!"**
>If this happens, ensure you have a clean install of Inventory Inventor, and if the problem persists, [let me know](#contacting-me)!

Contacting Me
--------------
If you still have some questions or recommendations you'd like to throw my way, you can ask me on Discord (Joshuarox100#5024) or leave a suggestion or issue on the [GitHub](https://github.com/Joshuarox100/VRC-Inventory-Inventor/issues) page.
