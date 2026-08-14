using System.Text;

namespace MogwaiNano.Engine
{
    public class ServerMessage
    {
        public string Source { get; set; }
        
        public string Function { get; set; }
        
        public string[] Parameters { get; set; }

        public ServerMessage()
        {
            Source = string.Empty;
            Function = string.Empty;
            Parameters = new string[0];
        }

        public ServerMessage(string source, string function, params string[] parameters)
        {
            Source = source;
            Function = function;
            Parameters = parameters;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
           
            sb.AppendLine($"Source: {Source}"); 
            sb.AppendLine($"Function: {Function}"); 

            foreach (var param in Parameters)
                sb.AppendLine($"- {param}");

            return sb.ToString();   
        }
    }
}
