# Keyboard shortcuts

Fallout Script Studio is fully usable with the keyboard. The tables below list every shortcut that is bound in the application; they are generated from the real
bindings (`EditorCommands`, the `InputBindings` of the windows) and a test fails when a binding is added or removed without this file being updated.
To regenerate the tables run the tests with the environment variable `FSS_WRITE_SHORTCUTS=1`.

## Main window

| Shortcut | Action |
| --- | --- |
| `Alt+E` | Toggle function tree |
| `Alt+L` | Toggle log |
| `Alt+M` | Check Message Text Structure |
| `Alt+P` | Go to Procedure... |
| `Alt+R` | Toggle right browser |
| `Alt+Shift+G` | Open All Include Files |
| `Ctrl++` | Zoom In |
| `Ctrl+-` | Zoom Out |
| `Ctrl+0` | Reset Zoom |
| `Ctrl+Break` | Cancel the running operation |
| `Ctrl+F` | Find in Files... |
| `Ctrl+F3` | Quick Find |
| `Ctrl+F8` | Next problem |
| `Ctrl+G` | Go to Line... |
| `Ctrl+H` | Find and Replace Bar |
| `Ctrl+N` | New Script |
| `Ctrl+O` | Open... |
| `Ctrl+P` | Create procedure |
| `Ctrl+PageDown` | Next document |
| `Ctrl+PageUp` | Previous document |
| `Ctrl+S` | Save |
| `Ctrl+Shift+F8` | Previous problem |
| `Ctrl+Shift+S` | Save All |
| `Ctrl+Shift+Tab` | Previous document |
| `Ctrl+Tab` | Next document |
| `Ctrl+W` | Close |
| `F3` | Find Next |
| `F4` | Preprocess |
| `F5` | Round Trip |
| `F7` | Scripts List Editor |
| `F8` | Compile Script |
| `Shift+F3` | Find Previous |

## Code editor

| Shortcut | Action |
| --- | --- |
| `Alt+A` | Align selected text |
| `Alt+C` | Comment text |
| `Alt+F` | Format code |
| `Alt+F12` | Find references |
| `Alt+G` | Open include file |
| `Alt+Left` | Back |
| `Alt+Q` | Decrease indent |
| `Alt+Right` | Forward |
| `Alt+S` | Split document |
| `Alt+Space` | Show completion list |
| `Alt+X` | Uncomment text |
| `Ctrl+*` | Toggle block comment |
| `Ctrl+/` | Toggle comment |
| `Ctrl+B` | Go to matching bracket |
| `Ctrl+F12` | Go to declaration |
| `Ctrl+Shift+A` | Upper case |
| `Ctrl+Shift+D` | Delete to end of line |
| `Ctrl+Shift+W` | Capitalize |
| `Ctrl+Shift+Z` | Lower case |
| `Ctrl+Space` | Show completion list |
| `F12` | Go to definition |
| `F2` | Rename |

## Dialogue rules window

| Shortcut | Action |
| --- | --- |
| `Delete` | Remove rule |

## Flowchart window

| Shortcut | Action |
| --- | --- |
| `Alt+E` | Edit node |
| `Alt+H` | Hide selection |
| `Alt+U` | Unhide all |
| `F5` | Update |

## Macro rename preview window

| Shortcut | Action |
| --- | --- |
| `Ctrl+Enter` | Apply |

## Message editor window

| Shortcut | Action |
| --- | --- |
| `Alt+A` | Add next line |
| `Alt+C` | Insert comment |
| `Alt+O` | Toggle player line |
| `Alt+S` | Send line |
| `Ctrl++` | Increase font size |
| `Ctrl+-` | Decrease font size |
| `Ctrl+D` | Duplicate rows |
| `Ctrl+N` | New |
| `Ctrl+O` | Open... |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As... |
| `Ctrl+Shift+Z` | Redo |
| `Ctrl+Y` | Redo |
| `Ctrl+Z` | Undo |
| `Enter` | Find Next |
| `F3` | Find Next |
| `F5` | Reload |
| `F8` | Go to next issue |
| `Shift+Enter` | Find Previous |
| `Shift+F3` | Find Previous |
| `Shift+F8` | Go to previous issue |

## Node code editor window

| Shortcut | Action |
| --- | --- |
| `Ctrl+Y` | Redo |
| `Ctrl+Z` | Undo |

## Project settings window

| Shortcut | Action |
| --- | --- |
| `Enter` | Add project define |

## Register script window

| Shortcut | Action |
| --- | --- |
| `Enter` | Find Next |

## Settings window

| Shortcut | Action |
| --- | --- |
| `Enter` | Add preprocessor define |

## User function window

| Shortcut | Action |
| --- | --- |
| `Ctrl+Enter` | Accept |

## Keys handled by the controls

These keys are not bindings of a window: the controls themselves handle them. The tests do not check this part.

### Dialogs

| Key | Action |
| --- | --- |
| Enter | Activates the default button of the dialog (OK, Save, Go...) |
| Esc | Cancels the dialog; in a tool window without a Cancel button it closes the window |
| Alt + the underlined letter | Activates the button, check box or option that has that access key |
| Tab, Shift+Tab | Moves the focus to the next or to the previous control |
| Ctrl+Enter | Accepts the user function, rename and similar dialogs from inside a multi-line box |

### Menus

| Key | Action |
| --- | --- |
| Alt, F10 | Activates the menu bar; the underlined letter of a menu opens it |
| Left, Right, Up, Down | Moves between the menus and their items; Enter chooses, Esc closes |
| Menu key, Shift+F10 | Opens the context menu of the control that has the focus |

### Procedures, Variables, Functions and Solution panels

| Key | Action |
| --- | --- |
| Up, Down, Left, Right, Home, End | Moves in the tree, expands and collapses |
| Enter | Procedures and Variables: goes to the declaration. Functions: inserts the function. Solution: opens the file. A folder or a group is expanded or collapsed |
| Ctrl+Enter | Procedures and Variables: inserts the name at the caret |
| F2 | Procedures and Variables: renames the symbol. Functions: renames the category |
| Delete | Procedures: deletes the procedure. Functions: deletes the function or the empty category |
| Alt+Up, Alt+Down | Procedures: moves the procedure up or down |
| Ctrl+C | Solution: copies the full path of the file |

### Errors, Problems and search results (also Find in Files)

| Key | Action |
| --- | --- |
| Up, Down | Moves to the previous or to the next row |
| Enter | Goes to the place of the row in the editor |
| Ctrl+C | Copies the row |
| Esc | Stops the running build |

### Search bar and Go to procedure

| Key | Action |
| --- | --- |
| Enter, Shift+Enter | Finds the next or the previous match; in the replace box, replaces |
| Esc | Closes the search bar or the list |
| Down | In the filter of the Go to procedure list, moves into the list |

### Message grid

| Key | Action |
| --- | --- |
| Enter | Edits the current cell |
| Delete, Backspace | Starts editing with an empty cell (Esc brings the old text back) |
| Ctrl+Enter, Shift+Enter | Inserts an empty line below or above; inside a message text it inserts a line break |
| Ctrl+Delete | Deletes the selected rows |
| Ctrl+Up, Ctrl+Down | Moves the selected rows |
| Ctrl+F | Goes to the search box |
| Esc | Leaves the search box, the details pane or the window |

### Dialogue preview

| Key | Action |
| --- | --- |
| Enter | Follows the selected option |
| Ctrl+Enter | Shows the code of the selected row in the editor |
| F2 | Edits the message of the selected row |

### Flowchart canvas

| Key | Action |
| --- | --- |
| Ctrl+A | Selects all the nodes |
| Ctrl+0, Ctrl+Plus, Ctrl+Minus | Resets, increases or decreases the zoom |
| Delete | Deletes the selected nodes |
| Esc | Clears the selection |
