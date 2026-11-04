namespace Deepwave.RetroOS.UI
{
    // Every icon an app definition, menu row or list can pick without its own sprite.
    // Values 0-12 keep their order (serialized in older assets); the rest is the high-res set.
    public enum BuiltinIcon
    {
        // --- original set: do not reorder (serialized in assets) ---
        File = 0,
        Computer = 1,
        Terminal = 2,
        Camera = 3,
        Mail = 4,
        Globe = 5,
        Photos = 6,
        Disc = 7,
        Folder = 8,
        Flag = 9,
        Clock = 10,
        Movie = 11,
        Note = 12,

        // --- system ---
        Monitor = 13,
        Drive = 14,
        Floppy = 15,
        CdRom = 16,
        Chip = 17,
        Server = 18,
        Battery = 19,
        Power = 20,

        // --- files ---
        FolderOpen = 21,
        Book = 22,
        Database = 23,
        Trash = 24,
        TrashFull = 25,

        // --- network ---
        Network = 26,
        Phone = 27,
        Download = 28,
        Refresh = 29,
        Home = 30,
        Search = 31,

        // --- media ---
        Music = 32,
        Speaker = 33,
        Cassette = 34,
        Microphone = 35,
        Paint = 36,

        // --- peripherals ---
        Printer = 37,
        Keyboard = 38,
        Mouse = 39,
        Joystick = 40,

        // --- productivity ---
        Calendar = 41,
        Calculator = 42,
        Contacts = 43,
        Chart = 44,
        Settings = 45,
        Tools = 46,
        Star = 47,

        // --- security ---
        Key = 48,
        Lock = 49,
        Shield = 50,
        Eye = 51,

        // --- dialogs ---
        Warning = 52,
        Error = 53,
        Info = 54,
        Question = 55,

        // --- games / story ---
        Bomb = 56,
        Cards = 57,
        Dice = 58,
        Ghost = 59,
        Skull = 60,

        // --- browser ---
        Back = 61,
        Forward = 62,
        Stop = 63,
        Bookmark = 64,
        BrokenImage = 65,
        Reload = 66,

        // --- programs ---
        Board = 67,
        Picture = 68,
        Explorer = 69,
        FreeCell = 70
    }
}
