﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DIGNDB.App.SmitteStop.Core.Contracts;
using DIGNDB.App.SmitteStop.Core.Helpers;
using DIGNDB.App.SmitteStop.Domain.Db;
using DIGNDB.App.SmitteStop.Domain.Dto;

namespace DIGNDB.App.SmitteStop.Core.Services
{
    public class ExposureKeyMapper : IExposureKeyMapper
    {
        /*
         * Constant to convert second into 10min interval
         * Because rolling start needs to be stored as increments of 10mins
        */
        private const int secTo10min = 60 * 10;
        List<TemporaryExposureKey> IExposureKeyMapper.FromDtoToEntity(TemporaryExposureKeyBatchDto dto)
        {
            return dto.keys.Select(key => new TemporaryExposureKey()
            {
                CreatedOn = DateTime.UtcNow,
                RollingStartNumber = key.rollingStart.ToUnixEpoch(),
                KeyData = key.key,
                RollingPeriod = (long)(key.rollingDurationSpan.TotalMinutes / 10.0d),
                TransmissionRiskLevel = key.transmissionRiskLevel,
                DaysSinceOnsetOfSymptoms = key.daysSinceOnsetOfSymptoms
            }).ToList();
        }

        public IList<TemporaryExposureKey> FilterDuplicateKeys(IList<TemporaryExposureKey> incomingKeys, IList<TemporaryExposureKey> exisitingKeys)
        {
            if (exisitingKeys.Count < 1)
                return incomingKeys;
            var uniqueKeys = new List<TemporaryExposureKey>();

            foreach (var key in incomingKeys)
            {
                if (!CheckIncomingKeyExists(key.KeyData, exisitingKeys))
                {
                    uniqueKeys.Add(key);
                }
            }
            return uniqueKeys;
        }

        private bool CheckIncomingKeyExists(byte[] incomingKey, IList<TemporaryExposureKey> existingKeys)
        {
            foreach (var item in existingKeys)
            {
                if (StructuralComparisons.StructuralEqualityComparer.Equals(incomingKey, item.KeyData))
                    return true;
            }

            return false;
        }
        public Domain.Proto.TemporaryExposureKey FromEntityToProto(TemporaryExposureKey source)
        {
            return new Domain.Proto.TemporaryExposureKey(
                    source.KeyData,
                    (int)source.RollingStartNumber / secTo10min,
                    (int)source.RollingPeriod,
                    (int)source.TransmissionRiskLevel
                );
        }

        public Domain.Proto.TemporaryExposureKeyExport FromEntityToProtoBatch(IList<TemporaryExposureKey> dtoKeys)
        {
            var keysByTime = dtoKeys.OrderBy(k => k.CreatedOn);
            var startTimes = new DateTimeOffset(keysByTime.First().CreatedOn);
            var endTimes = new DateTimeOffset(keysByTime.Last().CreatedOn);
            var batch = new Domain.Proto.TemporaryExposureKeyExport
            {
                BatchNum = 1,
                BatchSize = 1,
                StartTimestamp = (ulong)(startTimes.ToUnixTimeSeconds()),
                EndTimestamp = (ulong)(endTimes.ToUnixTimeSeconds()),
                Region = "DK"
            };
            batch.Keys.AddRange(dtoKeys.Select(x => FromEntityToProto(x)).ToList());

            return batch;
        }
    }
}
