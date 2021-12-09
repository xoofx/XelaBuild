
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace BuildServer.Tasks
{
    public class CacheBuilder : Task
    {
        [Required]
        public ITaskItem[] AssemblyReferences { get; set; }


        /// <summary>
        /// Hash of the ItemsToHash ItemSpec.
        /// </summary>
        [Output]
        public ITaskItem[] CacheFiles { get; set; }



        public override bool Execute()
        {

            foreach (var assemblyRef in AssemblyReferences)
            {
            }
            return true;
        }
    }
}