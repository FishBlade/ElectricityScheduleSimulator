using SmartHomeSystem.Backend.Function;

namespace SmartHomeSystem.Backend.Framework
{
    public enum NodeType
    {
        BaseConsumer,
        BaseProducer,
        BaseBattery,
        Line,
        Unknown,
        Root
    }

    #region 设备基类

    //设备各功率使用KW来计算

    public abstract class BaseDevice
    {
        public Guid id;
        public string name = "未命名设备";

        //设备名称
        public double performanceRating = 1.0;

        public bool switchon = false;

        public NodeType type;

        //##属性
        protected double randomfluctuations = 0;

        //设备ID
        private static readonly Random rand = new Random();//随机数生成器

        //随机波动

        private bool breakdowned = false;

        //耐久度
        private double formanceMutiplier = 1; //性能衰减倍率 难损0.1 正常1.0 易损5.0 极端20.0

        //是否损坏
        //开关
        public BaseDevice()
        {
            name = "新设备";
            id = Guid.NewGuid();
            formanceMutiplier = 1;
            Event.DailyEvent += formanceUpdate;
            Event.HourlyEvent += fluctuationUpdate;
            if (Framework.Topology.allService != null)
                Framework.Topology.allService.Add(id, this);
        }

        public BaseDevice(string label, double mutiplier)
        {
            name = label;
            id = Guid.NewGuid();
            formanceMutiplier = mutiplier;
            Event.DailyEvent += formanceUpdate;
            Event.HourlyEvent += fluctuationUpdate;
            if (Framework.Topology.allService != null)
                Framework.Topology.allService.Add(id, this);
        }

        ~BaseDevice()
        {
            if (Framework.Topology.allService != null)
                Framework.Topology.allService.Remove(id);
        }

        public void fluctuationUpdate()//随机波动--每小时
        {
            if (!breakdowned)
                randomfluctuations = (rand.NextDouble() * 2 - 1) / 100;//-1%~+1%
            else
                randomfluctuations = 0;
        }

        public void formanceUpdate()//性能衰减--每日
        {
            if (!breakdowned && switchon)
            {
                double fluctuation = (rand.NextDouble() / 10 * 4 + 0.1) * formanceMutiplier / 100;//0.1%-0.5%
                performanceRating *= 1 - fluctuation;//每日性能衰减0.1%-0.5%不等
                if (rand.NextDouble() / 5 > performanceRating) breakdowned = true; //设备损坏
                if (performanceRating < 0) performanceRating = 0; //防溢出
            }
        }

        public double GetCurrentPower()
        {
            double power = 0;
            if (this.breakdowned = true) return 0;
            if (this is Consumer)
                power += (this as Consumer)!.realpowerConsumption;
            if (this is Producer)
                power -= (this as Producer)!.realpowerProduction;
            return power;
        }
    }

    #endregion 设备基类

    #region 接口

    public interface Battery
    {
        double capacity { get; set; } //容量，单位Wh
        double currentCharge { get; set; }
        double realcapacity { get; }//

        //当前电量，单位Wh
        bool usingbattery { get; set; }

        public abstract void BatteryformanceUpdate();

        public abstract void Charge(double amount); //充电

        public abstract void Discharge(double amount); //放电

        //是否正在使用电池供电
    }

    public interface Consumer
    {
        double powerConsumption { get; set; } //功率消耗
        public double realpowerConsumption { get; } //实际功率消耗

        public abstract void ConsumerformanceUpdate();
    }

    public interface Producer
    {
        double powerProduction { get; set; } //功率产出
        public double realpowerProduction { get; } //实际功率产出

        public abstract void ProducerformanceUpdate();
    }

    public interface Sensor<T>
    {
        T? data { get; set; }
        public string dataLabel { get; set; }
        T? realData { get; set; }

        public virtual T GetData() { throw new NotImplementedException("返回值，但逻辑未定义"); }

        public abstract void SensorformanceUpdate();
    }

    #endregion 接口

    #region 模板类

    public class BaseBattery : BaseDevice, Battery
    {
        public BaseBattery() : base()
        {
            type = NodeType.BaseBattery;
            capacity = 0;
            realcapacity = capacity;
            currentCharge = capacity;
            usingbattery = false;
            Event.HourlyEvent += BatteryformanceUpdate;
        }

        public BaseBattery(string label, double mutiplier, double cap) : base(label, mutiplier)
        {
            type = NodeType.BaseBattery;
            capacity = cap;
            realcapacity = capacity;
            currentCharge = capacity;
            usingbattery = false;
            Event.HourlyEvent += BatteryformanceUpdate;
        }

        public double capacity { get; set; }
        public double currentCharge { get; set; }
        public double realcapacity { get; set; }
        public bool usingbattery { get; set; }

        public void BatteryformanceUpdate()//模拟老化带来的容量损失
        {
            realcapacity = capacity * performanceRating * (1 + randomfluctuations);
            if (currentCharge > realcapacity) currentCharge = realcapacity;
        }

        public void Charge(double amount)
        {
            currentCharge += amount;
            if (currentCharge > realcapacity) currentCharge = realcapacity;
        }

        public void Discharge(double amount)
        {
            currentCharge -= amount;
            if (currentCharge < 0) currentCharge = 0;
        }
    }

    public class BaseConsumer : BaseDevice, Consumer//标准用电器类
    {
        public BaseConsumer() : base()
        {
            type = NodeType.BaseConsumer;
            powerConsumption = 0;
            realpowerConsumption = 0;
            Event.HourlyEvent += ConsumerformanceUpdate;
        }

        public BaseConsumer(string label, double mutiplier, double powerconsumption) : base(label, mutiplier)
        {
            type = NodeType.BaseConsumer;
            powerConsumption = powerconsumption;
            realpowerConsumption = powerConsumption;
            Event.HourlyEvent += ConsumerformanceUpdate;
        }

        public double powerConsumption { get; set; }

        public double realpowerConsumption { get; set; }

        public void ConsumerformanceUpdate()
        {
            realpowerConsumption = powerConsumption * performanceRating * (1 + randomfluctuations);
            if (realpowerConsumption < 0) realpowerConsumption = 0;
        }
    }

    public class BaseProducer : BaseDevice, Producer//标准发电器类
    {
        public BaseProducer() : base()
        {
            type = NodeType.BaseProducer;
            powerProduction = 0;
            realpowerProduction = powerProduction;
            Event.HourlyEvent += ProducerformanceUpdate;
        }

        public BaseProducer(string label, double mutiplier, double p) : base(label, mutiplier)
        {
            type = NodeType.BaseProducer;
            powerProduction = p;
            realpowerProduction = powerProduction;
            Event.HourlyEvent += ProducerformanceUpdate;
        }

        public double powerProduction { get; set; }
        public double realpowerProduction { get; set; }

        public void ProducerformanceUpdate()
        {
            realpowerProduction = powerProduction * performanceRating * (1 + randomfluctuations);
            if (realpowerProduction < 0) realpowerProduction = 0;
        }
    }

    #endregion 模板类
}