"""SQLite migration, restart and demo-seed checks using disposable databases.
Run after dotnet build. EF_TOOL points to the dotnet-ef executable (version 10).
"""
import contextlib
import json
import os
from pathlib import Path
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
DLL = ROOT / 'bin/Debug/net10.0/AlpacaMedic.dll'
EF_TOOL = os.environ.get('EF_TOOL', '/tmp/alpacamedic-ef/dotnet-ef')


@contextlib.contextmanager
def server(database):
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0))
        port = sock.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    with tempfile.TemporaryFile() as log:
        process = subprocess.Popen(['dotnet', str(DLL), f'--Urls={base}',
                                    f'--DatabasePath={database}', '--Logging:LogLevel:Default=Warning'],
                                   cwd=ROOT, stdout=log, stderr=log)
        try:
            for _ in range(150):
                try:
                    with urllib.request.urlopen(base + '/api/business-assets', timeout=1):
                        break
                except (urllib.error.URLError, TimeoutError):
                    if process.poll() is not None:
                        log.seek(0)
                        raise AssertionError(log.read().decode())
                    time.sleep(0.1)
            else:
                raise AssertionError('Server did not start')

            def request(path, method='GET', body=None, status=None):
                req = urllib.request.Request(base + path, method=method,
                    data=None if body is None else json.dumps(body).encode(),
                    headers={} if body is None else {'Content-Type': 'application/json'})
                try:
                    response = urllib.request.urlopen(req, timeout=5)
                except urllib.error.HTTPError as error:
                    response = error
                with response:
                    assert response.status == status if status is not None else 200 <= response.status < 300
                    return None if response.status == 204 else json.load(response)
            yield request
        finally:
            process.terminate()
            process.wait(timeout=10)


with tempfile.TemporaryDirectory(prefix='alpacamedic-storage-', dir='/tmp') as directory:
    legacy = Path(directory) / 'legacy.db'
    env = dict(os.environ, DatabasePath=str(legacy))
    result = subprocess.run([EF_TOOL, 'database', 'update', 'InitialCreate', '--no-build',
                             '--connection', f'Data Source={legacy}'], cwd=ROOT, env=env,
                            capture_output=True, text=True)
    assert result.returncode == 0, result.stdout + result.stderr
    with sqlite3.connect(legacy) as db:
        db.executescript("""
            INSERT INTO BusinessProcesses VALUES (91, 'Legacy process', 'Preserve description', 3, 3, 3, '5.0');
            INSERT INTO BusinessAssets VALUES (95, 91, 'Legacy asset', 'Preserve asset');
            INSERT INTO SystemAssets (Id, Name, Description, AssetType, Zone, BlastRadius, BlastRadiusReason)
            VALUES (92, 'Legacy server', 'Preserve server', 'Server', 'DMZ', '2.5', 'Manual assessment'),
                   (93, 'Legacy cloud', '', 'Cloud', '', '1.0', ''),
                   (94, 'Legacy account', '', 'PersonAccount', '', '1.0', '');
            INSERT INTO AssetMappings VALUES (96, 95, 92);
            INSERT INTO SystemAccess VALUES (97, 92, 93, 'Preserve access');
            INSERT INTO AttackTechniques VALUES (1, 'T1190', 'Legacy technique', 'Initial Access', 'Legacy description', 'https://attack.mitre.org/techniques/T1190/');
            INSERT INTO SystemThreats VALUES (98, 92, 1, 'Preserve scenario', 3);
            """)
    with server(legacy) as request:
        relations = request('/api/business-asset-relations')
        assert relations == [{'id': 96, 'businessAssetId': 95, 'systemAssetId': 92,
                              'relationType': 'DependsOn', 'description': ''}], relations
        systems = {s['id']: s for s in request('/api/system-assets')}
        assert len(systems) == 4, 'Only create the network required by the old Zone value'
        network = next(s for s in systems.values() if s['name'] == 'DMZ')
        assert network['assetType'] == 'Network'
        migrated_locations = request('/api/system-asset-relations')
        assert len(migrated_locations) == 1
        assert migrated_locations[0]['sourceSystemAssetId'] == 92 and migrated_locations[0]['targetSystemAssetId'] == network['id']
        assert migrated_locations[0]['relationType'] == 'LocatedIn'
        assert all('zone' not in s for s in systems.values())
        assert [systems[i]['assetType'] for i in (92, 93, 94)] == ['Host', 'CloudService', 'Identity']
        assert systems[92]['description'] == 'Preserve server'
        assert 'blastRadius' not in systems[92]
        migrated = request('/api/attack-components')
        assert len(migrated) == 1 and migrated[0]['id'] == 98 and migrated[0]['comment'] == 'Preserve scenario'
        assert request('/api/risks') == []
        assert request('/api/attack-surface') == []
        surface = request('/api/attack-surface', 'POST', {'systemAssetId': 92, 'isExposed': True,
                          'exposureType': 'Internet', 'description': 'Public entry'}, status=201)
        repeated = request('/api/attack-surface', 'POST', dict(surface, exposureType='RemoteAccess'), status=200)
        assert repeated['id'] == surface['id']
        surface = request('/api/attack-surface/' + str(surface['id']), 'PUT', dict(repeated, isExposed=False, description='Retained reason'), status=200)
        assert request('/api/attack-surface?systemAssetId=92') == [surface]
        for exposure in ['Internet', 'RemoteAccess', 'InternalNetwork', 'UserInteraction', 'ThirdParty', 'Cloud', 'Physical', 'Other']:
            surface = request('/api/attack-surface/' + str(surface['id']), 'PUT', dict(surface, exposureType=exposure), status=200)
        request('/api/attack-surface/999999', 'PUT', surface, status=404)
        for invalid in [dict(surface, systemAssetId=999999), dict(surface, exposureType='Invalid'), dict(surface, description=None)]:
            request('/api/attack-surface', 'POST', invalid, status=400)
        second = request('/api/attack-surface', 'POST', dict(surface, systemAssetId=93), status=201)
        request('/api/attack-surface/' + str(second['id']), 'PUT', dict(second, systemAssetId=92), status=400)
        request('/api/attack-surface/' + str(second['id']), 'DELETE', status=204)
        assert request('/api/risks') == []
        business = request('/api/business-asset-relations/96', 'PUT', {
            'businessAssetId': 95, 'systemAssetId': 92, 'relationType': 'StoredIn', 'description': 'Updated business relation'})
        system = request('/api/system-asset-relations', 'POST', {
            'sourceSystemAssetId': 92, 'targetSystemAssetId': 93,
            'relationType': 'Uses', 'description': 'Persisted system relation'})
    with sqlite3.connect(legacy) as db:
        assert not db.execute("SELECT name FROM sqlite_master WHERE name = 'AssetMappings'").fetchall()
        assert db.execute('PRAGMA foreign_key_check').fetchall() == []
    with server(legacy) as request:
        assert request('/api/attack-surface') == [surface]
        assert request('/api/business-asset-relations') == [business]
        assert request('/api/system-asset-relations') == migrated_locations + [system]
        assert request('/api/risks') == []
    shared_database = Path(directory) / 'shared.db'
    result = subprocess.run([EF_TOOL, 'database', 'update', 'AssetModel', '--no-build',
                             '--connection', f'Data Source={shared_database}'], cwd=ROOT,
                            env=dict(os.environ, DatabasePath=str(shared_database)), capture_output=True, text=True)
    assert result.returncode == 0, result.stdout + result.stderr
    with sqlite3.connect(shared_database) as db:
        db.executescript("""
            INSERT INTO BusinessProcesses VALUES (10, 'First process', '', 3, 3, 3, '5.0'), (20, 'Second process', '', 1, 1, 1, '1.0');
            INSERT INTO BusinessAssets (Id, BusinessProcessId, Name, Description)
            VALUES (301, 10, 'Økonomidata', 'Asset description A'),
                   (302, 20, ' økonomidata ', 'Asset description B'),
                   (303, 10, 'ØKONOMIDATA', 'Asset description A'),
                   (304, 20, 'Patient Identity', '');
            INSERT INTO SystemAssets (Id, Name, Description, AssetType, Zone, BlastRadius, BlastRadiusReason)
            VALUES (92, 'DB service', '', 'Service', '', '1.0', ''), (93, 'Web app', '', 'Application', '', '1.0', '');
            INSERT INTO BusinessAssetRelations (Id, BusinessAssetId, SystemAssetId, RelationType, Description)
            VALUES (401, 301, 92, 'StoredIn', 'Relation description A'),
                   (402, 302, 92, 'StoredIn', 'Relation description B'),
                   (403, 303, 92, 'StoredIn', 'Relation description A'),
                   (404, 302, 93, 'ProcessedBy', 'Other target');
            """)
    with server(shared_database) as request:
        assets = request('/api/business-assets')
        assert len(assets) == 2, assets
        asset = next(a for a in assets if a['id'] == 301)
        assert asset['name'] == 'Økonomidata' and asset['businessProcessIds'] == [10, 20]
        assert asset['description'] == 'Asset description A\nAsset description B'
        relations = request('/api/business-asset-relations?businessAssetId=301')
        assert len(relations) == 2
        assert next(r for r in relations if r['id'] == 401)['description'] == 'Relation description A\nRelation description B'
        assert next(r for r in relations if r['id'] == 404)['systemAssetId'] == 93
        renamed = request('/api/business-assets/301', 'PUT', {'name': 'Fælles økonomidata'})
        assert renamed['description'] == asset['description']
        assert renamed['businessProcessIds'] == [10, 20]
        reused = request('/api/business-assets', 'POST', {'name': ' FÆLLES ØKONOMIDATA ', 'businessProcessId': 10})
        assert reused == renamed
        request('/api/business-assets/301', 'PUT', {'name': 'Patient Identity'}, status=400)
        assert request('/api/business-asset-relations?businessAssetId=301') == relations
        request('/api/business-assets/301/processes/20', 'DELETE', status=204)
        assert next(a for a in request('/api/business-assets') if a['id'] == 301)['businessProcessIds'] == [10]
        assert request('/api/business-asset-relations?businessAssetId=301') == relations
        request('/api/business-assets', 'POST', {'name': renamed['name'], 'businessProcessId': 20})
        request('/api/business-processes/10', 'DELETE', status=204)
        assert next(a for a in request('/api/business-assets') if a['id'] == 301)['businessProcessIds'] == [20]
    with server(shared_database) as request:
        assets = request('/api/business-assets')
        assert len(assets) == 2
        assert next(a for a in assets if a['id'] == 301)['name'] == 'Fælles økonomidata'
        assert next(a for a in assets if a['id'] == 301)['businessProcessIds'] == [20]
        assert request('/api/business-asset-relations?businessAssetId=301') == relations
    with sqlite3.connect(shared_database) as db:
        assert db.execute('PRAGMA foreign_key_check').fetchall() == []
        assert db.execute('SELECT COUNT(*) FROM BusinessAssets WHERE NormalizedName IS NULL').fetchone()[0] == 0
    zone_database = Path(directory) / 'zones.db'
    result = subprocess.run([EF_TOOL, 'database', 'update', 'SharedBusinessAssets', '--no-build',
                             '--connection', f'Data Source={zone_database}'], cwd=ROOT,
                            env=dict(os.environ, DatabasePath=str(zone_database)), capture_output=True, text=True)
    assert result.returncode == 0, result.stdout + result.stderr
    with sqlite3.connect(zone_database) as db:
        db.executescript("""
            INSERT INTO SystemAssets (Id, Name, Description, AssetType, Zone, BlastRadius, BlastRadiusReason)
            VALUES (10, 'First host', 'Preserve host', 'Host', ' DMZ ', '2.5', 'Manual reason'),
                   (11, 'DMZ', '', 'Network', 'DMZ', '1.0', ''),
                   (12, 'Second host', '', 'Host', 'dmz', '1.0', ''),
                   (13, 'Office host', '', 'Host', 'OfficeNetwork', '1.0', ''),
                   (14, 'Service without zone', '', 'Service', '   ', '1.0', '');
            INSERT INTO SystemAssetRelations (Id, SourceSystemAssetId, TargetSystemAssetId, RelationType, Description)
            VALUES (40, 10, 11, 'LocatedIn', 'Keep existing relation'), (41, 10, 13, 'ConnectsTo', 'Keep connection');
            """)
    with server(zone_database) as request:
        systems = request('/api/system-assets')
        assert len(systems) == 6
        assert all('zone' not in s for s in systems)
        office = next(s for s in systems if s['name'] == 'OfficeNetwork')
        assert office['assetType'] == 'Network'
        assert len([s for s in systems if s['name'] == 'DMZ']) == 1
        host = next(s for s in systems if s['id'] == 10)
        assert host['description'] == 'Preserve host' and 'blastRadius' not in host
        relations = request('/api/system-asset-relations')
        assert len(relations) == 4
        assert next(r for r in relations if r['id'] == 40)['description'] == 'Keep existing relation'
        assert next(r for r in relations if r['id'] == 41)['relationType'] == 'ConnectsTo'
        assert any(r['sourceSystemAssetId'] == 12 and r['targetSystemAssetId'] == 11 and r['relationType'] == 'LocatedIn' for r in relations)
        assert any(r['sourceSystemAssetId'] == 13 and r['targetSystemAssetId'] == office['id'] and r['relationType'] == 'LocatedIn' for r in relations)
        assert all(r['sourceSystemAssetId'] != r['targetSystemAssetId'] for r in relations)
        assert not any(r['sourceSystemAssetId'] == 14 for r in relations)
    with server(zone_database) as request:
        assert request('/api/system-assets') == systems
        assert request('/api/system-asset-relations') == relations
    with sqlite3.connect(zone_database) as db:
        assert 'Zone' not in {column[1] for column in db.execute('PRAGMA table_info(SystemAssets)')}
        assert db.execute('PRAGMA foreign_key_check').fetchall() == []
    fresh = Path(directory) / 'fresh.db'
    with server(fresh) as request:
        assets = request('/api/business-assets')
        systems = request('/api/system-assets')
        business = request('/api/business-asset-relations')
        relations = request('/api/system-asset-relations')
        assert {a['name'] for a in assets} == {'Clinical Patient Data', 'Patient Identity', 'Adverse Event Data', 'Research Data', 'Employee Identity'}
        assert {s['name'] for s in systems} == {'PatientWebApp', 'WEB01', 'PostgreSQLDB01', 'DB01', 'FreeIPA', 'IPA01', 'WAZUH', 'WAZUH01', 'DMZ', 'OfficeNetwork', 'OfficePC', 'Firewall', 'CloudMail'}
        names = {s['id']: s['name'] for s in systems}
        triples = {(names[r['sourceSystemAssetId']], r['relationType'], names[r['targetSystemAssetId']]) for r in relations}
        assert {('PatientWebApp', 'HostedOn', 'WEB01'), ('PatientWebApp', 'ConnectsTo', 'PostgreSQLDB01'),
                ('PatientWebApp', 'AuthenticatesVia', 'FreeIPA'), ('PostgreSQLDB01', 'HostedOn', 'DB01'),
                ('FreeIPA', 'HostedOn', 'IPA01'), ('WAZUH', 'HostedOn', 'WAZUH01'),
                ('WEB01', 'LocatedIn', 'DMZ'), ('DB01', 'LocatedIn', 'DMZ'), ('IPA01', 'LocatedIn', 'DMZ')} <= triples
    with server(fresh) as request:
        assert request('/api/business-assets') == assets
        assert request('/api/system-assets') == systems
        assert request('/api/business-asset-relations') == business
        assert request('/api/system-asset-relations') == relations
print('PASS: Attack Surface validation, unique asset upsert, all exposure types, deletion, restart persistence; Zone labels migrate to network assets and LocatedIn without duplicate or self relations; legacy mappings, types and component comments migrate; shared Unicode assets consolidate with memberships and relations; renames and relation edits survive restart; demo seed is complete and idempotent.')
