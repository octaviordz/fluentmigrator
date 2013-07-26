#region License

// 
// Copyright (c) 2007-2009, Sean Chambers <schambers80@gmail.com>
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentMigrator.Exceptions;
using FluentMigrator.Infrastructure;

namespace FluentMigrator.Runner
{
    public interface IMigrationInformationLoader
    {
        SortedList<long, IMigrationInfo> LoadMigrations();
    }

    public class DefaultMigrationInformationLoader : IMigrationInformationLoader
    {
        public DefaultMigrationInformationLoader(IMigrationConventions conventions, Assembly assembly, string @namespace,
                                                 IEnumerable<string> tagsToMatch)
            : this(conventions, assembly, @namespace, false, tagsToMatch)
        {
        }

        public DefaultMigrationInformationLoader(IMigrationConventions conventions, Assembly assembly, string @namespace,
                                                 bool loadNestedNamespaces, IEnumerable<string> tagsToMatch)
            : this(
                conventions,
                new List<MigrationAssemblyInfo>() { new MigrationAssemblyInfo() { Assembly = assembly, Namespace = @namespace } },
                false,
                tagsToMatch)
        {
        }

        public DefaultMigrationInformationLoader(
            IMigrationConventions conventions,
            ICollection<MigrationAssemblyInfo> assemblies,
            bool loadNestedNamespaces,
            IEnumerable<string> tagsToMatch)
        {
            Conventions = conventions;
            Assemblies = assemblies;
            LoadNestedNamespaces = loadNestedNamespaces;
            TagsToMatch = tagsToMatch ?? new string[] { };
        }

        public IMigrationConventions Conventions { get; private set; }
        public ICollection<MigrationAssemblyInfo> Assemblies { get; private set; }

        public bool LoadNestedNamespaces { get; private set; }
        public IEnumerable<string> TagsToMatch { get; private set; }

        public SortedList<long, IMigrationInfo> LoadMigrations()
        {
            var migrationInfos = new SortedList<long, IMigrationInfo>();

            IEnumerable<IMigration> migrationList = FindMigrations();

            if (migrationList == null)
                return migrationInfos;

            foreach (IMigration migration in migrationList)
            {
                IMigrationInfo migrationInfo = Conventions.GetMigrationInfo(migration);
                if (migrationInfos.ContainsKey(migrationInfo.Version))
                    throw new DuplicateMigrationException(String.Format("Duplicate migration version {0}.",
                                                                        migrationInfo.Version));
                migrationInfos.Add(migrationInfo.Version, migrationInfo);
            }

            return migrationInfos;
        }

        private IEnumerable<IMigration> FindMigrations()
        {
            foreach (var migrationAssemblyInfo in Assemblies)
            {
                IEnumerable<Type> matchedTypes = migrationAssemblyInfo.Assembly.GetExportedTypes()
                                                     .Where(t => Conventions.TypeIsMigration(t)
                                                                 &&
                                                                 (Conventions.TypeHasMatchingTags(t, TagsToMatch) ||
                                                                  !Conventions.TypeHasTags(t)));
                var Namespace = migrationAssemblyInfo.Namespace;
                if (!string.IsNullOrEmpty(Namespace))
                {
                    Func<Type, bool> shouldInclude = t => t.Namespace == Namespace;
                    if (LoadNestedNamespaces)
                    {
                        string matchNested = Namespace + ".";
                        shouldInclude = t => t.Namespace == Namespace || t.Namespace.StartsWith(matchNested);
                    }

                    matchedTypes = matchedTypes.Where(shouldInclude);
                }


                var migrations = matchedTypes.Select(
                        matchedType => (IMigration)matchedType.Assembly.CreateInstance(matchedType.FullName));
                foreach (var migration in migrations)
                {
                    yield return migration;
                }
            }
        }
    }
}