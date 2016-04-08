# Dark Basic Parser - Unit 22 - Assignment 2
Parses any DarkBasic Classic document and spews it into a data dictionary, example below:

![Here's an example of the output it generates](https://i.gyazo.com/6d82211d869d6f01c1ed0f17e7816b3e.png)

Please note that at the time of writing, the code is very messy and was rushed (if you count 7 - 10 hours as rushing...)
I will (hopefully) refactor the majority of it, and finish moving stuff around so that it is more reusable.

# How to use it
Ensure that any files that the main file uses (other source code) is next to the main source file.
Simply drag and drop the main .dba file onto the executable (after building it). Any included files will also be searched through and added to the data dictionary.

# Important note on detection
I was pretty lazy and didn't bother to allow anything but lowercase constructs to be detected. Function/subroutines of your own can have any name, as long as the prefix "function", and the suffixes "endfunction", and "return" are lowercase. This is the same for any other construct: "if", "for", "endif", "gosub", etc...
