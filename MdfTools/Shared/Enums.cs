namespace MdfTools.Shared
{
    public enum DataType
    {
        Unknown,
        Unsigned,
        Signed,
        Float,
        AnsiString,
        ByteArray,
        Bool
    }

    public enum ByteOrder
    {
        Intel,
        Motorola,
        Default,
        Undefined
    }

    public enum ValueConversionType
    {
        Identity,
        Linear,
        Rational3
    }

    internal enum BlockId : ushort
    {
        MdfBlockAT = 'A' + ('T' << 8), // ##AT Attachment Block
        MdfBlockCA = 'C' + ('A' << 8), // ##CA Decodable Array Block
        MdfBlockCC = 'C' + ('C' << 8), // ##CC Decodable Conversion Block
        MdfBlockCG = 'C' + ('G' << 8), // ##CG Decodable Group Block
        MdfBlockCH = 'C' + ('H' << 8), // ##CH Decodable Hierarchy Block
        MdfBlockCN = 'C' + ('N' << 8), // ##CN Decodable Block
        MdfBlockDG = 'D' + ('G' << 8), // ##DG Data Group Block
        MdfBlockDI = 'D' + ('I' << 8), // ##DG Data Invalidation Block
        MdfBlockDL = 'D' + ('L' << 8), // ##DL Data List Block
        MdfBlockDT = 'D' + ('T' << 8), // ##DT Data Block
        MdfBlockDV = 'D' + ('V' << 8), // ##DV Data Values Block
        MdfBlockDZ = 'D' + ('Z' << 8), // ##DZ Data Zipped Block
        MdfBlockEV = 'E' + ('V' << 8), // ##EV Event Block
        MdfBlockFH = 'F' + ('H' << 8), // ##FH File History Block
        MdfBlockHD = 'H' + ('D' << 8), // ##HD Header Block
        MdfBlockHL = 'H' + ('L' << 8), // ##HL Header List Block
        MdfBlockLD = 'L' + ('D' << 8), // ##LD List Data Block
        MdfBlockMD = 'M' + ('D' << 8), // ##MD Meta Data Block
        MdfBlockRD = 'R' + ('D' << 8), // ##RD Reduction Data Block
        MdfBlockRI = 'R' + ('I' << 8), // ##RD Reduction Data Invalidation Block
        MdfBlockRV = 'R' + ('V' << 8), // ##RD Reduction Values Block
        MdfBlockSD = 'S' + ('D' << 8), // ##SD Signal Data Block
        MdfBlockSI = 'S' + ('I' << 8), // ##SI Source Information Block
        MdfBlockSR = 'S' + ('R' << 8), // ##SR Signal Reduction Block
        MdfBlockTX = 'T' + ('X' << 8)  // ##TX Text Block
    }
}
