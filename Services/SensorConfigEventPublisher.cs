using System.Text.Json;
using Confluent.Kafka;
using SensorManager.Dtos;

namespace SensorManager.Services;

public interface ISensorConfigEventPublisher
{
    Task PublishAsync(SensorConfigChangedEvent ev, CancellationToken ct);
}

public class SensorConfigEventPublisher : ISensorConfigEventPublisher
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public SensorConfigEventPublisher(IProducer<string, string> producer, IConfiguration config)
    {
        _producer = producer;
        _topic = config["Kafka:ConfigTopic"]
                ?? config["Kafka__ConfigTopic"]
                ?? "sensor-config-events";
    }

    public Task PublishAsync(SensorConfigChangedEvent ev, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(ev);
        return _producer.ProduceAsync(
            _topic,
            new Message<string, string> { Key = ev.SensorId, Value = json },
            ct);
    }
}
