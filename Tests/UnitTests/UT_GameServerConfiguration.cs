/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.IO;
using DOL.Database.Connection;
using DOL.GS;
using NUnit.Framework;

namespace DOL.Tests.Unit.Gameserver
{
    [TestFixture]
    public class UT_GameServerConfiguration
    {
        private static string WriteConfigToTempFile(string xmlContent)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xml");
            File.WriteAllText(tempFile, xmlContent);
            return tempFile;
        }

        [Test]
        public void LoadFromXml_WithEmptyDbType_DefaultsToSqlite()
        {
            const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root><Server><DBType></DBType></Server></root>";
            var filePath = WriteConfigToTempFile(xml);

            try
            {
                var config = new GameServerConfiguration();
                config.LoadFromXMLFile(new FileInfo(filePath));

                Assert.That(config.DBType, Is.EqualTo(EConnectionType.DATABASE_SQLITE));
            }
            finally
            {
                File.Delete(filePath);
            }
        }

        [Test]
        [TestCase("DATABASE_SQLITE")]
        [TestCase(" sqlite ")]
        public void LoadFromXml_WithDatabasePrefixedEnum_ParsesAsSqlite(string input)
        {
            string xml = $"<?xml version=\"1.0\" encoding=\"utf-8\"?><root><Server><DBType>{input}</DBType></Server></root>";
            var filePath = WriteConfigToTempFile(xml);

            try
            {
                var config = new GameServerConfiguration();
                config.LoadFromXMLFile(new FileInfo(filePath));

                Assert.That(config.DBType, Is.EqualTo(EConnectionType.DATABASE_SQLITE));
            }
            finally
            {
                File.Delete(filePath);
            }
        }
    }
}
