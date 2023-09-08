using System.Reflection;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Npgsql;
using static System.Runtime.InteropServices.JavaScript.JSType;

if (args.Length != 1)
{
    Console.WriteLine("Please provide the configuration file path as a command line argument");
}

string path = "";

if (args.Length <= 0)
{
    path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)    +"/getting-started.properties";
}
else
{
     path = args[0];
    Console.WriteLine("ARGS: " + args[0]);
}

Console.WriteLine("PATH: " + path);

IConfiguration configuration = new ConfigurationBuilder()
    .AddIniFile(path)
    .Build();

configuration["group.id"] = "kafka-dotnet-getting-started";
configuration["auto.offset.reset"] = "earliest";

const string topic = "orders";

CancellationTokenSource cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // prevent the process from terminating.
    cts.Cancel();
};

Console.WriteLine("Ini process");

AppContext.SetSwitch("Npgsql.EnableStoredProcedureCompatMode", true);

string conDB = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONN_DB")) ? "User ID=postgres;Password=postgres;Host=localhost;Port=5432;Database=vest;Pooling=true;Connection Lifetime=0;" : Environment.GetEnvironmentVariable("CONN_DB");

NpgsqlConnection connDB = new NpgsqlConnection(conDB);

using (var consumer = new ConsumerBuilder<string, string>(
        configuration.AsEnumerable()).Build())
{
    consumer.Subscribe(topic);
    try
    {
        while (true)
        {

            var cr = consumer.Consume(cts.Token);

            Console.WriteLine($"Consumed event from topic {topic} with key {cr.Message.Key,-10} and value {cr.Message.Value}");
            JToken jTokenKey = JToken.Parse(cr.Message.Key);
            JToken jTokenVal = JToken.Parse(cr.Message.Value);

            string? id = jTokenKey["id"]?.ToString();
            string? id_user = jTokenVal["id_user"]?.ToString();
            string? transaction_type = jTokenVal["transaction_type"]?.ToString();
            string? stock_symbol = jTokenVal["stock_symbol"]?.ToString();
            int? stock_unit = Convert.ToInt32(jTokenVal["stock_units"]?.ToString());
            double? stock_price = Convert.ToDouble(jTokenVal["stock_price"]?.ToString());
            DateTime? date_transaction = Convert.ToDateTime(jTokenVal["date_transaction"]?.ToString());

            connDB.Open();
            try
            {
                using (var cmd = new NpgsqlCommand("f_create_registro", connDB))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@p_id", NpgsqlTypes.NpgsqlDbType.Varchar, id);
                    cmd.Parameters.AddWithValue("@p_id_user", NpgsqlTypes.NpgsqlDbType.Varchar, id_user);
                    cmd.Parameters.AddWithValue("@p_transaction_type", NpgsqlTypes.NpgsqlDbType.Varchar, transaction_type);
                    cmd.Parameters.AddWithValue("@p_stock_symbol", NpgsqlTypes.NpgsqlDbType.Varchar, stock_symbol);
                    cmd.Parameters.AddWithValue("@p_stock_units", NpgsqlTypes.NpgsqlDbType.Integer, stock_unit * (transaction_type.ToUpper().Equals("SL") ? -1 : 1));
                    cmd.Parameters.AddWithValue("@p_stock_price", NpgsqlTypes.NpgsqlDbType.Real, stock_price);
                    cmd.Parameters.AddWithValue("@p_date_transaction", NpgsqlTypes.NpgsqlDbType.Timestamp, date_transaction);
                    var rep = await cmd.ExecuteScalarAsync();

                    if (rep != null)
                    {
                        JToken jToken = JToken.Parse(rep.ToString());

                        if (!string.IsNullOrEmpty(jToken["error"].ToString()))
                        {
                            throw new Exception($"Error al guardar en BD: {jToken["error"].ToString()}");
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error DB store: " + ex.Message);
            }
            finally
            {
                if (connDB.State == System.Data.ConnectionState.Open)
                {
                    connDB.Close();
                }
            }
        }
    }
    catch (OperationCanceledException ez)
    {
        Console.WriteLine("Error: " + ez.Message);
        // Ctrl-C was pressed.
    }
    finally
    {
        consumer.Close();
    }
}
