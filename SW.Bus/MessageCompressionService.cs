using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace SW.Bus
{
    public class MessageCompressionService 
    {
        public Task<byte[]> Compress(string message, Encoding encoding) =>Compress(encoding.GetBytes(message));
        
        public async Task<byte[]> Compress(byte[] message)
        {
            await using var mStream = new MemoryStream();
            await using var gStream = new GZipStream(mStream, CompressionLevel.Optimal);
            await gStream.WriteAsync(message);
            gStream.Close();
            return mStream.ToArray();
        }

        public async Task<byte[]> DeCompress(byte[] message)
        {
            await using var mStream = new MemoryStream(message);
            await using var gStream = new GZipStream(mStream, CompressionMode.Decompress);
            await using var resultStream = new MemoryStream();
            await gStream.CopyToAsync(resultStream);
            return resultStream.ToArray();
        }

        public async Task<string> DeCompress(byte[] message, Encoding encoding)
        {
            var bytes = await DeCompress(message);
            return encoding.GetString(bytes);
        }
    }
}