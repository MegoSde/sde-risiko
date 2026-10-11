"""HTTP integration checks. Run against a disposable, running instance."""
import json
import sys
import urllib.request
import urllib.error

BASE = (sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:7070').rstrip('/')


def request(path, method='GET', data=None, status=200):
    body = None if data is None else json.dumps(data).encode()
    req = urllib.request.Request(BASE + path, data=body, method=method,
                                 headers={} if body is None else {'Content-Type': 'application/json'})
    try:
        response = urllib.request.urlopen(req)
    except urllib.error.HTTPError as error:
        response = error
    raw = response.read()
    assert response.status == status, (method, path, response.status, raw.decode())
    if response.headers.get_content_type() == 'application/json':
        return json.loads(raw) if raw else None
    return raw.decode()


process_ids, system_ids, asset_ids = [], [], []
try:
    pages = ['dashboard', 'business-analysis', 'asset-model', 'attack-surface',
             'threat-analysis', 'controls', 'risk-analysis', 'risk-matrix', 'cis-priorities', 'report']
    for page in pages:
        html = request('/' + page)
        assert 'aria-current="page"' in html
        assert '/js/' + page + '.js' in html
        request('/js/' + page + '.js')
    request('/css/site.css')
    request('/js/common.js')
    request('/unknown-page', status=404)
    assert '<h1>Asset Model</h1>' in request('/asset-mapping')
    request('/api/asset-mappings', status=404)
    for endpoint in ['attack-surface', 'business-processes', 'business-assets', 'system-assets', 'business-asset-relations', 'system-asset-relations',
                     'attack-components', 'attack-connections', 'attack-techniques', 'cis-controls',
                     'technique-controls', 'component-controls', 'risks', 'cis-priorities', 'dashboard']:
        request('/api/' + endpoint)
    assert len(request('/api/cis-controls')) == 18
    assert len(request('/api/attack-techniques')) >= 3
    for c in range(1, 4):
        for i in range(1, 4):
            for a in range(1, 4):
                process = request('/api/business-processes', 'POST', {
                    'name': 'Smoke CIA', 'confidentiality': c, 'integrity': i, 'availability': a,
                    'consequence': 99}, 201)
                process_ids.append(process['id'])
                expected = round((2 * (c+i+a)/3 - 1) * 2) / 2
                assert process['consequence'] == expected, process
    process = request('/api/business-processes/' + str(process['id']), 'PUT', {
        'name': 'Smoke patientbehandling', 'confidentiality': 3, 'integrity': 3, 'availability': 3})
    assert process['consequence'] == 5
    request('/api/business-processes', 'POST', {'name': 'Invalid', 'confidentiality': 4}, 400)
    request('/api/business-processes', 'POST', {'name': '   '}, 400)
    assets = [request('/api/business-assets', 'POST', {
        'name': 'Smoke patientdata ' + str(n), 'businessProcessId': process['id']}, 201) for n in range(2)]
    asset_ids.extend(a['id'] for a in assets)
    request('/api/business-assets/' + str(assets[0]['id']), 'PUT', {
        'name': 'Smoke journaler', 'businessProcessId': process['id']})
    request('/api/business-assets', 'POST', {'name': 'Invalid', 'businessProcessId': -1}, 400)
    second_process = request('/api/business-processes', 'POST', {'name': 'Smoke shared asset process'}, 201)
    process_ids.append(second_process['id'])
    shared = request('/api/business-assets', 'POST', {'name': ' SMOKE JOURNALER ', 'businessProcessId': second_process['id'], 'description': 'Do not overwrite'}, 201)
    assert shared['id'] == assets[0]['id'] and shared['name'] == 'Smoke journaler'
    assert shared['businessProcessIds'] == sorted([process['id'], second_process['id']])
    again = request('/api/business-assets', 'POST', {'name': 'smoke journaler', 'businessProcessId': second_process['id']}, 201)
    assert again == shared
    request('/api/business-assets/' + str(shared['id']), 'PUT', {'name': assets[1]['name']}, 400)
    request('/api/business-assets/' + str(shared['id']), 'PUT', {'name': '   '}, 400)
    renamed = request('/api/business-assets/' + str(shared['id']), 'PUT', {'name': 'Smoke shared renamed'})
    assert renamed['businessProcessIds'] == shared['businessProcessIds']
    request('/api/business-assets/' + str(shared['id']) + '/processes/' + str(second_process['id']), 'DELETE', status=204)
    assert next(a for a in request('/api/business-assets') if a['id'] == shared['id'])['businessProcessIds'] == [process['id']]
    request('/api/business-assets/' + str(shared['id']) + '/processes/' + str(second_process['id']), 'DELETE', status=404)
    request('/api/business-assets', 'POST', {'name': 'Smoke shared renamed', 'businessProcessId': second_process['id']}, 201)
    request('/api/business-processes/' + str(second_process['id']), 'DELETE', status=204)
    process_ids.remove(second_process['id'])
    assert next(a for a in request('/api/business-assets') if a['id'] == shared['id'])['businessProcessIds'] == [process['id']]

    system = request('/api/system-assets', 'POST', {
        'name': 'Smoke WEB01', 'assetType': 'Host',
        'description': 'Adgang til patientdata'}, 201)
    system_ids.append(system['id'])
    target = request('/api/system-assets', 'POST', {'name': 'Smoke DB01', 'assetType': 'Host'}, 201)
    system_ids.append(target['id'])
    request('/api/system-assets', 'POST', {'name': 'Invalid', 'assetType': 'Invented'}, 400)
    for asset in assets:
        business_relation = request('/api/business-asset-relations', 'POST', {
            'businessAssetId': asset['id'], 'systemAssetId': system['id']}, 201)
    relation_data = {'businessAssetId': assets[-1]['id'], 'systemAssetId': system['id'], 'relationType': 'DependsOn'}
    request('/api/business-asset-relations', 'POST', relation_data, 400)
    other = request('/api/business-asset-relations', 'POST', dict(relation_data, relationType='StoredIn'), 201)
    request('/api/business-asset-relations/' + str(other['id']), 'PUT', relation_data, 400)
    other = request('/api/business-asset-relations/' + str(other['id']), 'PUT', dict(relation_data, relationType='UsedBy', description='Updated'), 200)
    assert other['description'] == 'Updated'
    assert {r['businessAssetId'] for r in request('/api/business-asset-relations?businessAssetId=' + str(assets[-1]['id']))} == {assets[-1]['id']}
    for invalid in [dict(relation_data, businessAssetId=-1), dict(relation_data, systemAssetId=-1), dict(relation_data, relationType='HostedOn'), dict(relation_data, relationType=''), dict(relation_data, relationType=None)]:
        request('/api/business-asset-relations', 'POST', invalid, 400)
    request('/api/business-asset-relations/999999', 'PUT', relation_data, 400)  # Existing triple is rejected first.
    request('/api/business-asset-relations/999999', 'PUT', dict(relation_data, relationType='Other'), 404)
    request('/api/business-asset-relations/' + str(other['id']), 'DELETE', status=204)
    system_relation_data = {'sourceSystemAssetId': system['id'], 'targetSystemAssetId': target['id'], 'relationType': 'ConnectsTo'}
    system_relation = request('/api/system-asset-relations', 'POST', system_relation_data, 201)
    request('/api/system-asset-relations', 'POST', system_relation_data, 400)
    reverse = request('/api/system-asset-relations', 'POST', dict(system_relation_data, sourceSystemAssetId=target['id'], targetSystemAssetId=system['id']), 201)
    same_pair = request('/api/system-asset-relations', 'POST', dict(system_relation_data, relationType='DependsOn'), 201)
    request('/api/system-asset-relations/' + str(same_pair['id']), 'PUT', system_relation_data, 400)
    request('/api/system-asset-relations/' + str(same_pair['id']), 'DELETE', status=204)
    system_relation = request('/api/system-asset-relations/' + str(system_relation['id']), 'PUT', dict(system_relation_data, relationType='Uses', description='Changed'), 200)
    assert system_relation['description'] == 'Changed'
    assert request('/api/system-asset-relations?sourceSystemAssetId=' + str(system['id'])) == [system_relation]
    for invalid in [dict(system_relation_data, sourceSystemAssetId=-1), dict(system_relation_data, targetSystemAssetId=-1), dict(system_relation_data, targetSystemAssetId=system['id']), dict(system_relation_data, relationType='StoredIn'), dict(system_relation_data, relationType=''), dict(system_relation_data, relationType=None)]:
        request('/api/system-asset-relations', 'POST', invalid, 400)
        request('/api/system-asset-relations/' + str(system_relation['id']), 'PUT', invalid, 400)
    request('/api/system-asset-relations/999999', 'PUT', system_relation_data, 404)
    request('/api/system-asset-relations/' + str(reverse['id']), 'DELETE', status=204)
    assert not any(r['id'] == reverse['id'] for r in request('/api/system-asset-relations'))
    summary = next(s for s in request('/api/system-assets') if s['id'] == system['id'])
    assert 'zone' not in summary
    request('/api/business-asset-relations/' + str(business_relation['id']), 'DELETE', status=204)
    assert request('/api/risks') == []
    request('/api/business-assets/' + str(assets[0]['id']), 'DELETE', status=204)
    asset_ids.remove(assets[0]['id'])
    request('/api/system-assets/' + str(target['id']), 'DELETE', status=204)
    system_ids.remove(target['id'])
    assert not any(r['id'] == system_relation['id'] for r in request('/api/system-asset-relations'))
    print('PASS: Asset Model relation CRUD, filters, direction, validation and cascades; 10 pages, static assets, catalogs, CRUD, all CIA combinations, validation,')
    print('      catalogs and cascades.')
finally:
    for identity in system_ids:
        request('/api/system-assets/' + str(identity), 'DELETE', status=204)
    for identity in process_ids:
        request('/api/business-processes/' + str(identity), 'DELETE', status=204)
    for identity in asset_ids:
        request('/api/business-assets/' + str(identity), 'DELETE', status=204)
