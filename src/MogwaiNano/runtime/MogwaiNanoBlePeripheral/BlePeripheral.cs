// Copyright 2026 Stéphane Sibué
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using MogwaiNano.Engine;
using MogwaiNano.Objects;
using nanoFramework.Device.Bluetooth;
using nanoFramework.Device.Bluetooth.GenericAttributeProfile;
using System;
using System.Collections;
using System.Diagnostics;

namespace MogwaiNanoBlePeripheral
{
    public class BlePeripheral
    {
        public const string EVENT_BLE_PERIPHERAL_DID_START = "BLE_PERIPHERAL_DID_START";
        public const string EVENT_BLE_PERIPHERAL_DID_STOP = "BLE_PERIPHERAL_DID_STOP";
        public const string EVENT_BLE_PERIPHERAL_VALUE_DID_CHANGE = "BLE_PERIPHERAL_VALUE_DID_CHANGE";

        private static BlePeripheral _instance;

        private BluetoothLEServer _server = BluetoothLEServer.Instance;

        public ServiceDefinition FirstService { get; private set; }

        public Hashtable ServicesByName { get; private set; } = new();
        
        public Hashtable CharacteristicsByName { get; private set; } = new();
        
        public Hashtable ServicesByUuid { get; private set; } = new();
        
        public Hashtable CharacteristicsByUuid { get; private set; } = new();
        
        public static BlePeripheral Current
        {
            get
            {
                if (_instance == null)
                    _instance = new BlePeripheral();

                return _instance;
            }
        }

        public string Name => _server?.DeviceName ?? "Unknown";

        public static Error BlePeripheralUnableToCreateError { get; } = new Error("BLE.1", "unable to create peripheral error");

        private BlePeripheral()
        {

        }

        public EvalResult CreatePeripheralFromRecordDefinition(MOGRecord record, string primitiveName)
        {
            var engine = record.Engine;

            Reset();

            // Récupération des données

            var name = record.GetItem("name") as MOGString;
            var services = record.GetItem("services") as MOGList;

            // Validation des données du record

            if (name == null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, primitiveName, "name: is mandatory");

            if (services == null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, primitiveName, "services: is mandatory");

            // Récupération du name

            _server.DeviceName = name.Value;

            // Création des services et des caractéristiques à partir de la définition du record

            try
            {
                foreach (var service in services.Items)
                {
                    if (service is not MOGRecord serviceRecord)
                        return EvalResult.Failure(engine, Error.BadArgumentValueError, primitiveName, "services: each service must be a record");

                    var s = CreateServiceFromRecord(serviceRecord);

                    ServicesByName[s.Name] = s;
                    ServicesByUuid[s.Uuid] = s;

                    if (FirstService == null)
                        FirstService = s;
                }

                if (ServicesByName.Count == 0)
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, primitiveName, $"you must define at least one service");

                // On liste toute les characteristics gérées

                foreach (string key in CharacteristicsByUuid.Keys)
                {
                    var c = CharacteristicsByUuid[key] as CharacteristicDefinition;
                    Debug.WriteLine($"characteritic {c.Name} - {c.Uuid}");
                }

                // Mise en route des objets BLE à partir des définitions

                foreach (ServiceDefinition service in ServicesByName.Values)
                {
                    var serviceProviderResult = GattServiceProvider.Create(service.Uuid);

                    if (serviceProviderResult.Error != BluetoothError.Success)
                        return EvalResult.Failure(engine, BlePeripheralUnableToCreateError, primitiveName, $"Failed to create GattServiceProvider for service {service.Name} with UUID {service.Uuid}", $"Error: {serviceProviderResult.Error}");

                    service.ServiceProvider = serviceProviderResult.ServiceProvider;

                    foreach (CharacteristicDefinition characteristic in service.Characteristics.Values)
                    {
                        var characteristicParameters = new GattLocalCharacteristicParameters
                        {
                            CharacteristicProperties = GattCharacteristicProperties.None,
                            WriteProtectionLevel = GattProtectionLevel.Plain,
                            ReadProtectionLevel = GattProtectionLevel.Plain,
                            UserDescription = characteristic.Name
                        };

                        foreach (char c in characteristic.Properties.ToUpper())
                        {
                            if (c == 'R')
                                characteristicParameters.CharacteristicProperties |= GattCharacteristicProperties.Read;
                            else if (c == 'W')
                                characteristicParameters.CharacteristicProperties |= GattCharacteristicProperties.Write;
                            else if (c == 'X')
                                characteristicParameters.CharacteristicProperties |= GattCharacteristicProperties.WriteWithoutResponse;
                            else if (c == 'I')
                                characteristicParameters.CharacteristicProperties |= GattCharacteristicProperties.Indicate;
                            else if (c == 'N')
                                characteristicParameters.CharacteristicProperties |= GattCharacteristicProperties.Notify;
                        }

                        var characteristicResult = service.ServiceProvider.Service.CreateCharacteristic(characteristic.Uuid, characteristicParameters);

                        if (characteristicResult.Error != BluetoothError.Success)
                            return EvalResult.Failure(engine, BlePeripheralUnableToCreateError, primitiveName, $"Failed to create GattLocalCharacteristic for characteristic {characteristic.Name} with UUID {characteristic.Uuid}.", $"Error: {characteristicResult.Error}");

                        characteristic.RealCharacteristic = characteristicResult.Characteristic;
                        characteristic.RealCharacteristic.ReadRequested += CharacteristicInstance_ReadRequested;
                        characteristic.RealCharacteristic.WriteRequested += CharacteristicInstance_WriteRequested;
                    }                   
                }

                return EvalResult.NoError;
            }
            catch (Exception ex)
            {
                return EvalResult.Failure(engine, Error.BadArgumentValueError, primitiveName, ex.Message);
            }
        }

        public void Reset()
        {
            Debug.WriteLine("BLE PERIPHERAL RESET");

            if (FirstService != null)
            {
                if (FirstService.ServiceProvider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started)
                {
                    FirstService.ServiceProvider.StopAdvertising();
                    FirstService.Engine.FireEvent(EVENT_BLE_PERIPHERAL_DID_STOP);
                }

                FirstService = null;
            }

            foreach (CharacteristicDefinition characteristic in CharacteristicsByName.Values)
            {
                characteristic.RealCharacteristic.ReadRequested -= CharacteristicInstance_ReadRequested;
                characteristic.RealCharacteristic.WriteRequested -= CharacteristicInstance_WriteRequested;
                characteristic.RealCharacteristic = null;
            }

            CharacteristicsByName.Clear();
            CharacteristicsByUuid.Clear();

            ServicesByName.Clear();
            ServicesByUuid.Clear();
        }

        private void CharacteristicInstance_WriteRequested(GattLocalCharacteristic sender, GattWriteRequestedEventArgs WriteRequestEventArgs)
        {
            var key = sender.Uuid.ToString();
            var request = WriteRequestEventArgs.GetRequest();

            if (CharacteristicsByUuid.Contains(key))
            {
                var characteristic = CharacteristicsByUuid[key] as CharacteristicDefinition;

                var buffer = request.Value;
                var dataReader = DataReader.FromBuffer(buffer);
                var bytes = new byte[buffer.Length];

                dataReader.ReadBytes(bytes);
                characteristic.Value = new MOGData(characteristic.Engine, bytes);
                
                if (request.Option == GattWriteOption.WriteWithResponse)
                    request.Respond();

                var eventData = new MOGRecord(characteristic.Engine);
                eventData.SetItem("name", new MOGString(characteristic.Engine, characteristic.Name));
                eventData.SetItem("uuid", new MOGString(characteristic.Engine, characteristic.Uuid.ToString()));
                eventData.SetItem("value", characteristic.Value);

                characteristic.Engine.FireEvent(EVENT_BLE_PERIPHERAL_VALUE_DID_CHANGE, eventData);
            }
            else
            {
                request.RespondWithProtocolError(GattProtocolError.RequestNotSupported);
            }
        }

        private void CharacteristicInstance_ReadRequested(GattLocalCharacteristic sender, GattReadRequestedEventArgs ReadRequestEventArgs)
        {
            var key = sender.Uuid.ToString();
            var request = ReadRequestEventArgs.GetRequest();

            if (CharacteristicsByUuid.Contains(key))
            {
                var characteristic = CharacteristicsByUuid[key] as CharacteristicDefinition;              
                var buffer = new Buffer(characteristic.Value.Items);
                request.RespondWithValue(buffer);
            }
            else
            {
                request.RespondWithProtocolError(GattProtocolError.RequestNotSupported);
            }
        }

        private ServiceDefinition CreateServiceFromRecord(MOGRecord record)
        {
            var engine = record.Engine;

            // Récupération des données

            var name = record.GetItem("name") as MOGString;
            var uuid = record.GetItem("uuid") as MOGString;
            var characteristics = record.GetItem("characteristics") as MOGList;

            // Validation des données du record

            if (name == null)
                throw new ArgumentException("name: is mandatory");

            if (uuid == null)
                throw new ArgumentException("uuid: is mandatory");

            if (characteristics == null)
                throw new ArgumentException("characteristics: is mandatory");

            // Création du service

            Guid guid;

            if (!Guid.TryParseGuidWithDashes(uuid.Value, out guid))
                throw new ArgumentException("invalid service uuid");

            var service = new ServiceDefinition(engine, name.Value, guid);

            foreach (var ch in characteristics.Items)
            {
                if (ch is not MOGRecord characteristicRecord)
                    throw new ArgumentException("each characteristic must be a record");

                var characteristic = CreateCharacteristicFromRecord(service, characteristicRecord);
                service.Characteristics[characteristic.Name] = characteristic;

                CharacteristicsByName[characteristic.Name] = characteristic;
                CharacteristicsByUuid[characteristic.Uuid.ToString()] = characteristic;
            }

            return service;
        }

        private CharacteristicDefinition CreateCharacteristicFromRecord(ServiceDefinition service, MOGRecord record)
        {
            var engine = record.Engine;

            // Récupération des données

            var name = record.GetItem("name") as MOGString;
            var uuid = record.GetItem("uuid") as MOGString;
            var properties = record.GetItem("properties") as MOGString;
            var value = record.GetItem("value") as MOGData;

            // Validation des données du record

            if (name == null)
                throw new ArgumentException("name: is mandatory");

            if (uuid == null)
                throw new ArgumentException("uuid: is mandatory");

            if (properties == null)
                throw new ArgumentException("properties: is mandatory");

            if (value == null)
                throw new ArgumentException("value: is mandatory");

            // Création de la characteristic

            Guid guid;

            if (!Guid.TryParseGuidWithDashes(uuid.Value, out guid))
                throw new ArgumentException("invalid characteristic uuid");

            var characteristic = new CharacteristicDefinition(engine, service, name.Value, guid, properties.Value, value);

            return characteristic;
        }

        public class ServiceDefinition
        {
            public MogwaiNanoEngine Engine { get; }

            public string Name { get; }

            public Guid Uuid { get; }

            public Hashtable Characteristics { get; } = new();

            public GattServiceProvider ServiceProvider { get; set; }

            public GattLocalService RealService => ServiceProvider?.Service;

            public ServiceDefinition(MogwaiNanoEngine engine,  string name, Guid uuid)
            {
                Engine = engine;
                Name = name;
                Uuid = uuid;
            }
        }

        public class CharacteristicDefinition
        {
            public MogwaiNanoEngine Engine { get; }

            public ServiceDefinition Service { get; }

            public string Name { get; }

            public Guid Uuid { get; }

            public string Properties { get; }

            public MOGData Value { get; set; }

            public GattLocalCharacteristic RealCharacteristic { get; set; }

            public CharacteristicDefinition(MogwaiNanoEngine engine, ServiceDefinition service, string name, Guid uuid, string properties, MOGData value)
            {
                Engine = engine;
                Service = service;
                Name = name;
                Uuid = uuid;
                Properties = properties;
                Value = value;
            }
        }

        public static EvalResult PrimitiveBlePeripheralCreate(MogwaiNanoEngine engine, string name)
        {
            // record ble.peripheral.create

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGRecord))
            {
                var record = engine.StackPop() as MOGRecord;

                BlePeripheral.Current.Reset();

                var r = BlePeripheral.Current.CreatePeripheralFromRecordDefinition(record, name);

                if (r.IsError)
                    BlePeripheral.Current.Reset();

                return r;
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveBlePeripheralStart(MogwaiNanoEngine engine, string name)
        {
            // ble.peripheral.start

            if (Current.FirstService == null)
                return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"you must define at less one service");

            if (Current.FirstService.ServiceProvider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started)
                return EvalResult.NoError;

            var advParameters = new GattServiceProviderAdvertisingParameters
            {
                IsDiscoverable = true,
                IsConnectable = true
            };

            Current.FirstService.ServiceProvider.StartAdvertising(advParameters);
            
            engine.FireEvent(EVENT_BLE_PERIPHERAL_DID_START);

            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveBlePeripheralStop(MogwaiNanoEngine engine, string name)
        {
            BlePeripheral.Current.Reset();
            return EvalResult.NoError;
        }

        public static EvalResult PrimitiveBlePeripheralSetValue(MogwaiNanoEngine engine, string name)
        {
            // value 'name' ble.peripheral.setValue

            var s = engine.StackSign(2);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName) && s[1] == typeof(MOGData))
            {
                var cName = engine.StackPop() as MOGName;   
                var cValue = engine.StackPop() as MOGData;  

                if (BlePeripheral.Current.CharacteristicsByName.Contains(cName.Value))
                {
                    var characteristic = BlePeripheral.Current.CharacteristicsByName[cName.Value] as CharacteristicDefinition;         
                    characteristic.Value = cValue;
                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"Characteristic with name '{cName.Value}' not found.");
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveBlePeripheralGetValue(MogwaiNanoEngine engine, string name)
        {
            // 'name' ble.peripheral.getValue

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var cName = engine.StackPop() as MOGName;

                if (BlePeripheral.Current.CharacteristicsByName.Contains(cName.Value))
                {
                    var characteristic = BlePeripheral.Current.CharacteristicsByName[cName.Value] as CharacteristicDefinition;
                    engine.StackPush(characteristic.Value);
                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"Characteristic with name '{cName.Value}' not found.");
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

        public static EvalResult PrimitiveBlePeripheralNotify(MogwaiNanoEngine engine, string name)
        {
            // 'name' ble.peripheral.notify

            var s = engine.StackSign(1);

            if (s.Length == 0)
                return EvalResult.Failure(engine, Error.TooFewArgumentsError, name);

            if (s[0] == typeof(MOGName))
            {
                var cName = engine.StackPop() as MOGName;

                if (BlePeripheral.Current.CharacteristicsByName.Contains(cName.Value))
                {
                    var characteristic = BlePeripheral.Current.CharacteristicsByName[cName.Value] as CharacteristicDefinition;
                    
                    if (characteristic.RealCharacteristic.SubscribedClients.Length > 0)
                        characteristic.RealCharacteristic.NotifyValue(new Buffer(characteristic.Value.Items));
                    
                    return EvalResult.NoError;
                }
                else
                {
                    return EvalResult.Failure(engine, Error.BadArgumentValueError, name, $"Characteristic with name '{cName.Value}' not found.");
                }
            }

            return EvalResult.Failure(engine, Error.BadArgumentTypeError, name);
        }

    }
}
