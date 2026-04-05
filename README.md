Instructions: 

Requires the following libraries/packages: 
 - ManoloV02: DirextcTexWrapper - Licensed under the GPL-3.0 License  (https://github.com/MANOLOV02/DirectXTexWrapper )
 - Ousnius: NiflySharp – Licensed under the GPL-3.0 License (https://github.com/ousnius/NiflySharp)
 - Ousnius: Material Editor – Licensed under the MIT License (https://github.com/ousnius/Material-Editor)
 - OpenTK (GLControl) – Licensed under the MIT License (https://github.com/opentk/opentk)
 - ICSharpCode.SharpZipLib.dll – Licensed under the GPL-3.0 with linking exception (https://github.com/icsharpcode/SharpZipLib)
 - K4os.Compression.LZ4.Streams – Licensed under the MIT License (https://github.com/MiloszKrajewski/K4os.Compression.LZ4)

Changes to be made until is fixed int Niflysharp

i) in NiflySharp - Niffile.cs - Load:
.......


    try
    {
        // Create a new default instance of the block type
        if (blockTypeStr == "BSSkin::Instance") blockTypeStr = "BSSkin_Instance";  <<---ADDD
        if (blockTypeStr == "BSSkin::BoneData") blockTypeStr = "BSSkin_BoneData"; <<---ADDD
        if (blockTypeStr == "BSConnectPoint::Parents") blockTypeStr = "BSConnectPoint_Parents"; <<---ADDD
        if (blockTypeStr == "BSConnectPoint::Children") blockTypeStr = "BSConnectPoint_Children"; <<---ADDD
        var blockType = Type.GetType("NiflySharp.Blocks." + blockTypeStr);
        blockStreamable = Activator.CreateInstance(blockType) as INiStreamable;
    }
    catch
    {
        // Block type is unknown
        HasUnknownBlocks = true;
        block = new NiUnknown(streamReversible, Header.GetBlockSize(i));
    }

    if (blockStreamable != null)
    {
        // Read the block
        //streamReversible.Argument = null;
        blockStreamable.Sync(streamReversible);
        block = blockStreamable as NiObject;
    }

    if (block != null)
        Blocks.Add(block);
}
ii) In Niflysharp - Niheader.cs - Addblockinfo


  public void AddBlockInfo(INiObject newBlock)
  
  {
  
      string blockTypeName = newBlock.GetType().Name;  
      if (blockTypeName == "BSSkin_Instance") blockTypeName = "BSSkin::Instance";  <<---ADDD
      if (blockTypeName == "BSSkin_BoneData") blockTypeName = "BSSkin::BoneData";  <<---ADD     
      if (blockTypeName == "BSConnectPoint_Parents") blockTypeName = "BSConnectPoint::Parents";  <<---ADDD
      if (blockTypeName == "BSConnectPoint_Children") blockTypeName = "BSConnectPoint::Children";  <<---ADDD
      
      ushort blockTypeIndex = AddOrFindBlockTypeIndex(blockTypeName);
      
      blockTypeIndices.Add(blockTypeIndex);

      if (Version.FileVersion >= NiFileVersion.V20_2_0_5)
          blockSizes.Add(0);

      BlockCount++;
  }
