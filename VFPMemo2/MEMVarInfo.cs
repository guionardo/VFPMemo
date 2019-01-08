using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace VFPMemo
{
    public class MEMVarInfo
    {
        const double DELPHI_EPOCH = 2415019.0;
        static public readonly int HeaderSize;
        static MEMVarInfo()
        {
            HeaderSize = Marshal.SizeOf(new MemVarHeader());
        }

        /// <summary>
        /// header: TMEMVarHeader;
        /// </summary>
        public MemVarHeader header { get; private set; }

        /// <summary>
        /// null_t: AnsiChar;
        /// </summary>
        public string null_t;

        /// <summary>
        /// name  : AnsiString;
        /// </summary>
        public string name { get; private set; }

        /// <summary>
        /// value : Variant;
        /// </summary>
        public object value { get; private set; }


        public bool ReadStruct(Stream fs, out object value, Type t)
        {
            value = null;
            if (fs == null)
                return false;

            byte[] buffer =
                new byte[Marshal.SizeOf(t)];

            try
            {
                if (fs.Read(buffer, 0, Marshal.SizeOf(t)) != Marshal.SizeOf(t))
                    return false;
                GCHandle handle =
                    GCHandle.Alloc(buffer,
                    GCHandleType.Pinned);
                Object temp =
                    Marshal.PtrToStructure(
                    handle.AddrOfPinnedObject(),
                    t);
                handle.Free();
                value = temp;
                return true;
            }
            catch (Exception e)
            {
                throw new Exception("unespected MEM file format (problem reading header)", e);
            }
        }

        /// <summary>
        /// function ReadFromStream(stream: TStream): Boolean;  // false if EOF    
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public bool ReadFromStream(Stream stream)
        {
            name = "";
            value = null;

            /*
   name := '';  value := Unassigned;
   header_bytes_read := stream.Read(header, SizeOf(header));
   if header_bytes_read <> Sizeof(header) then begin
      if not ((header_bytes_read = 1) and (header.var_name[0] = #26)) then
         raise Exception.Create('unexpected MEM file format (problem reading header)');
      result := false;  // EOF
      EXIT;
   end;
   */


            if (!ReadStruct(stream, out object obj, typeof(MemVarHeader)))
                return false;

            MemVarHeader memVarHeader = (MemVarHeader)obj;

            // Nome da variável

            if (memVarHeader.var_name.Length == 0)
            {
                // long variable name
                memVarHeader.mem_type = memVarHeader.mem_type.ToString().ToLowerInvariant()[0];
                ushort name_length = 0;
                if (!ReadUInt16(stream, ref name_length) || name_length < 1)
                {
                    return false;
                }
                string ns = null;
                if (name_length > stream.Length - stream.Position)
                {
                    if (!ReadUntilNull(stream, ref ns))
                        return false;
                }
                else
                if (!ReadChar(stream, ref ns, name_length))
                    return false;
                name = ns;
            }
            else
            {
                name = memVarHeader.var_name;
            }



            /*
  // variable name
   if header.var_name[0] = #0 then begin  // long variable name
      assert(header.mem_type = LowerCase(header.mem_type));
      stream.ReadBuffer(name_length, Sizeof(name_length));
      SetLength(name, name_length);
      stream.ReadBuffer(name[1], name_length);
   end else begin
      assert(header.mem_type = UpCase(header.mem_type));
      name := header.var_name;
   end;
   */

            switch (memVarHeader.mem_type.ToString().ToUpperInvariant())
            {
                case "A":   // Array
                    ushort array_dim_1 = 0, array_dim_2 = 0;
                    if (ReadUInt16(stream, ref array_dim_1) &&
                        ReadUInt16(stream, ref array_dim_2) &&
                        array_dim_1 > 0)
                    {
                        List<object> elements = new List<object>();

                        for (int i = 0; i < array_dim_1 * Math.Max((int)array_dim_2, 1); i++)
                        {
                            MEMVarInfo mEl = new MEMVarInfo();
                            if (mEl.ReadFromStream(stream))
                                elements.Add(mEl.value);
                            else
                                elements.Add(null);
                        }

                        if (array_dim_2 == 0)
                        {
                            // Vetor
                            value = elements.ToArray();
                        }
                        else
                        {
                            if (ConvertTo2D(elements.ToArray(), out object[,] arrayValue, array_dim_1, array_dim_2))
                                value = arrayValue;
                            else
                                value = null;
                        }
                    }
                    /*
 stream.ReadBuffer(array_dim_1, SizeOf(array_dim_1));
            stream.ReadBuffer(array_dim_2, SizeOf(array_dim_2));
            if array_dim_2 = 0 then // it's a vector, not an array
               array_dim_2 := 1;
            SetLength(a, array_dim_1 * array_dim_2);
            for i := 0 to array_dim_1 * array_dim_2 - 1 do begin
               if not v.ReadFromStream(stream) then
                  raise Exception.Create('error reading array element');
               a[i] := v.value;
            end;
            value := a;
            */
                    break;
                case "0":
                    ReadChar(stream, ref null_t, 1);
                    value = null;
                    // stream.ReadBuffer(null_t, 1);  value := Null;
                    break;
                case "C":
                case "H":
                case "Q":
                    bool binary = false;
                    int text_length;
                    if (memVarHeader.mem_type.ToString().ToUpperInvariant() == "H")
                    {
                        binary = memVarHeader.width != 0;
                        text_length = (int)memVarHeader.big_size;
                    }
                    else
                    {
                        binary = memVarHeader.mem_type.ToString().ToUpper() == "Q";
                        text_length = memVarHeader.width;
                    }
                    if (binary)
                    {
                        byte[] q = new byte[text_length];
                        stream.Read(q, 0, text_length);
                        value = q;
                    }
                    else
                    {
                        string s = "";
                        if (ReadChar(stream, ref s, text_length))
                            value = s;
                        else
                            value = null;
                    }
                    /*
if UpCase(header.mem_type) = 'H' then begin // length > 254
               binary := header.width <> 0;
               text_length := header.big_size;
            end else begin
               binary := UpCase(header.mem_type) = 'Q';
               text_length := header.width;
            end;
            if binary then begin
               SetLength(q, text_length);  stream.ReadBuffer(q[0], text_length);  value := q;
            end else begin
               SetLength(c, text_length);  stream.ReadBuffer(c[1], text_length);  value := c;
            end;
            */

                    break;
                case "D":
                    double d = 0;
                    if (ReadDouble(stream, ref d))
                    {
                        value = DateTime.FromOADate(d - DELPHI_EPOCH).Date;
                    }

                    // 'D':  begin stream.ReadBuffer(d, Sizeof(d)); if d > 0 then d := d - DELPHI_EPOCH; VarCast(value, d, varDate); end;
                    break;
                case "L":
                    var b = stream.ReadByte();
                    value = b == 1;

                    //'L':  begin stream.ReadBuffer(l, Sizeof(l)); value:= l; end;
                    break;
                case "N":
                    double dn = 0;
                    if (ReadDouble(stream, ref dn))
                        value = dn;
                    //'N':  begin stream.ReadBuffer(n, Sizeof(n)); value:= n; end;
                    break;
                case "T":
                    double dt = 0;
                    if (ReadDouble(stream, ref dt))
                    {
                        value = DateTime.FromOADate(dt - DELPHI_EPOCH);
                    }
                    //'T':  begin stream.ReadBuffer(t, Sizeof(t)); if t > 0 then t := t - DELPHI_EPOCH; value:= t; end;
                    break;
                case "Y":
                    Int64 iy = 0;
                    if (ReadInt64(stream, ref iy))
                    {
                        value = (Decimal)iy / 10000;
                    }
                    // 'Y':  begin stream.ReadBuffer(y, Sizeof(y)); VarCast(value, y / 10000.0, varCurrency); end;
                    break;
                default:
                    throw new Exception("unexpected type \"" + memVarHeader.mem_type + "\" in MEM file");

            }
            /*
  // variable value
   case UpCase(header.mem_type) of
      'A':
         begin
            stream.ReadBuffer(array_dim_1, SizeOf(array_dim_1));
            stream.ReadBuffer(array_dim_2, SizeOf(array_dim_2));
            if array_dim_2 = 0 then // it's a vector, not an array
               array_dim_2 := 1;
            SetLength(a, array_dim_1 * array_dim_2);
            for i := 0 to array_dim_1 * array_dim_2 - 1 do begin
               if not v.ReadFromStream(stream) then
                  raise Exception.Create('error reading array element');
               a[i] := v.value;
            end;
            value := a;
         end;
      '0':  begin  stream.ReadBuffer(null_t, 1);  value := Null;  end;
      'C', 'H', 'Q':
         begin
            if UpCase(header.mem_type) = 'H' then begin // length > 254
               binary := header.width <> 0;
               text_length := header.big_size;
            end else begin
               binary := UpCase(header.mem_type) = 'Q';
               text_length := header.width;
            end;
            if binary then begin
               SetLength(q, text_length);  stream.ReadBuffer(q[0], text_length);  value := q;
            end else begin
               SetLength(c, text_length);  stream.ReadBuffer(c[1], text_length);  value := c;
            end;
         end;
      'D':  begin  stream.ReadBuffer(d, Sizeof(d));  if d > 0 then d := d - DELPHI_EPOCH;  VarCast(value, d, varDate);  end;
      'L':  begin  stream.ReadBuffer(l, Sizeof(l));  value := l;  end;
      'N':  begin  stream.ReadBuffer(n, Sizeof(n));  value := n;  end;
      'T':  begin  stream.ReadBuffer(t, Sizeof(t));  if t > 0 then t := t - DELPHI_EPOCH;  value := t;  end;
      'Y':  begin  stream.ReadBuffer(y, Sizeof(y));  VarCast(value, y / 10000.0, varCurrency);  end;
   else
      raise Exception.Create('unexpected type ''' + header.mem_type + ''' in MEM file');
   end;
   */


            return true;


            /*
var
   header_bytes_read: Integer;
   name_length: UInt16;
   text_length: UInt32;
   array_dim_1: UInt16;
   array_dim_2: UInt16;
   d: TDate;          // 64-bit double
   l: Boolean;
   n: Double;         // 64-bit double
   q: array of Byte;
   c: AnsiString;
   t: TDateTime;      // 64-bit double
   y: Int64;
   binary: Boolean;
   i: Cardinal;
   a: array of Variant;
   v: TMEMVarInfo;
begin

   result := true;
 
   // variable value
   case UpCase(header.mem_type) of
      'A':
         begin
            stream.ReadBuffer(array_dim_1, SizeOf(array_dim_1));
            stream.ReadBuffer(array_dim_2, SizeOf(array_dim_2));
            if array_dim_2 = 0 then // it's a vector, not an array
               array_dim_2 := 1;
            SetLength(a, array_dim_1 * array_dim_2);
            for i := 0 to array_dim_1 * array_dim_2 - 1 do begin
               if not v.ReadFromStream(stream) then
                  raise Exception.Create('error reading array element');
               a[i] := v.value;
            end;
            value := a;
         end;
      '0':  begin  stream.ReadBuffer(null_t, 1);  value := Null;  end;
      'C', 'H', 'Q':
         begin
            if UpCase(header.mem_type) = 'H' then begin // length > 254
               binary := header.width <> 0;
               text_length := header.big_size;
            end else begin
               binary := UpCase(header.mem_type) = 'Q';
               text_length := header.width;
            end;
            if binary then begin
               SetLength(q, text_length);  stream.ReadBuffer(q[0], text_length);  value := q;
            end else begin
               SetLength(c, text_length);  stream.ReadBuffer(c[1], text_length);  value := c;
            end;
         end;
      'D':  begin  stream.ReadBuffer(d, Sizeof(d));  if d > 0 then d := d - DELPHI_EPOCH;  VarCast(value, d, varDate);  end;
      'L':  begin  stream.ReadBuffer(l, Sizeof(l));  value := l;  end;
      'N':  begin  stream.ReadBuffer(n, Sizeof(n));  value := n;  end;
      'T':  begin  stream.ReadBuffer(t, Sizeof(t));  if t > 0 then t := t - DELPHI_EPOCH;  value := t;  end;
      'Y':  begin  stream.ReadBuffer(y, Sizeof(y));  VarCast(value, y / 10000.0, varCurrency);  end;
   else
      raise Exception.Create('unexpected type ''' + header.mem_type + ''' in MEM file');
   end;
end; 
*/

        }

        private bool ReadUntilNull(Stream stream, ref string ns)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                // Começou = sb.Length > 0
                bool fim = false;
                bool terminando = false;
                do
                {
                    var b = stream.ReadByte();
                    switch (b)
                    {
                        case -1:
                            fim = true;
                            break;
                        case 0:
                            // Continua até encontrar um caracter diferente de 0
                            terminando = true;
                            continue;
                        default:
                            if (terminando)
                            {
                                stream.Position = stream.Position - 1;
                                fim = true;
                            }
                            else
                                sb.Append((char)b);
                            break;
                    }
                }
                while (!fim);
                ns = sb.ToString();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ReadInt64(Stream stream, ref long iy)
        {
            try
            {
                byte[] buffer = new byte[8];
                stream.Read(buffer, 0, 8);
                iy = BitConverter.ToInt64(buffer, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ReadDouble(Stream stream, ref double d)
        {
            try
            {
                byte[] buffer = new byte[8];  // 64 bits
                stream.Read(buffer, 0, 8);
                d = BitConverter.ToDouble(buffer, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ReadChar(Stream stream, ref string value, int count)
        {
            try
            {
                byte[] buffer = new byte[count];
                stream.Read(buffer, 0, count);
                value = Encoding.GetEncoding(1252).GetString(buffer).TrimEnd(new char[] { ' ', '\0' });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ReadUInt16(Stream stream, ref ushort value)
        {
            try
            {
                value = (ushort)(stream.ReadByte() << 8 + stream.ReadByte());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ConvertTo2D(object[] a, out object[,] b, ushort nCols, ushort nRows)
        {
            b = null;
            if (a == null)
                throw new ArgumentNullException("a");
            if (nCols < 1)
                throw new ArgumentOutOfRangeException("nCols", "must be positive");
            if (nRows < 1)
                throw new ArgumentOutOfRangeException("nRows", "must be positive");
            if (a.Length != nCols * nRows)
                throw new Exception("Argument a has " + a.Length + " elements and can not generate " + nCols + " columns and " + nRows + " rows");

            b = new object[nCols, nRows];
            int c = 0, r = 0;
            foreach (var item in a)
            {
                b[c, r] = item;
                c++;
                if (c >= nCols)
                {
                    c = 0;
                    r++;
                }
            }
            return true;

        }
    }
}
