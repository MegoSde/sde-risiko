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


process_ids, system_ids = [], []
try:
    pages = ['dashboard', 'business-analysis', 'asset-mapping', 'system-analysis',
             'threat-analysis', 'controls', 'risk-analysis', 'risk-matrix', 'cis-priorities', 'report']
    for page in pages:
        html = request('/' + page)
        assert 'aria-current="page"' in html
        assert '/js/' + page + '.js' in html
        request('/js/' + page + '.js')
    request('/css/site.css')
    request('/js/common.js')
    request('/unknown-page', status=404)
    for endpoint in ['business-processes', 'business-assets', 'system-assets', 'asset-mappings',
                     'system-access', 'system-threats', 'attack-techniques', 'cis-controls',
                     'technique-controls', 'threat-controls', 'risks', 'risk-levels', 'cis-priorities', 'dashboard']:
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
    request('/api/business-assets/' + str(assets[0]['id']), 'PUT', {
        'name': 'Smoke journaler', 'businessProcessId': process['id']})
    request('/api/business-assets', 'POST', {'name': 'Invalid', 'businessProcessId': -1}, 400)
    system = request('/api/system-assets', 'POST', {
        'name': 'Smoke WEB01', 'assetType': 'Server', 'zone': 'DMZ',
        'blastRadius': 2.5, 'blastRadiusReason': 'Adgang til patientdata'}, 201)
    system_ids.append(system['id'])
    target = request('/api/system-assets', 'POST', {'name': 'Smoke DB01', 'assetType': 'Server'}, 201)
    system_ids.append(target['id'])
    request('/api/system-assets', 'POST', {'name': 'Invalid', 'blastRadius': 2.2}, 400)
    request('/api/system-assets', 'POST', {'name': 'Invalid', 'blastRadius': 4}, 400)
    request('/api/system-assets', 'POST', {'name': 'Invalid', 'assetType': 'Invented'}, 400)
    for asset in assets:
        mapping = request('/api/asset-mappings', 'POST', {
            'businessAssetId': asset['id'], 'systemAssetId': system['id']}, 201)
    duplicate = request('/api/asset-mappings', 'POST', {
        'businessAssetId': assets[-1]['id'], 'systemAssetId': system['id']})
    assert duplicate['id'] == mapping['id']
    summary = next(s for s in request('/api/system-assets') if s['id'] == system['id'])
    assert summary['directConsequence'] == 5, 'Do not count the same process twice'
    access = request('/api/system-access', 'POST', {
        'sourceSystemAssetId': system['id'], 'targetSystemAssetId': target['id'],
        'description': 'Servicekonto'}, 201)
    request('/api/system-access', 'POST', {
        'sourceSystemAssetId': system['id'], 'targetSystemAssetId': system['id']}, 400)
    access = request('/api/system-access', 'POST', {
        'sourceSystemAssetId': system['id'], 'targetSystemAssetId': target['id'], 'description': 'Opdateret'})
    assert access['description'] == 'Opdateret'
    technique = next(t for t in request('/api/attack-techniques') if t['techniqueId'] == 'T1190')
    threat_data = {'systemAssetId': system['id'], 'attackTechniqueId': technique['id'],
                   'scenario': 'Smoke SQL injection', 'likelihood': 3}
    threat = request('/api/system-threats', 'POST', threat_data, 201)
    risk_path = '/api/risks/' + str(threat['id'])
    risk = request(risk_path)
    assert risk['riskScore'] == 7.5 and risk['riskLevel'] == 'Medium'
    assert len(risk['businessProcesses']) == 1 and len(risk['businessAssets']) == 2
    assert {c['number'] for c in risk['cisControls']} == {7, 16, 18}
    control_path = '/api/threat-controls/' + str(threat['id']) + '/7'
    request(control_path, 'PUT', {'isRecommended': False, 'comment': 'Begrundet fravalg'})
    assert 7 not in [c['number'] for c in request(risk_path)['cisControls']]
    request(control_path, 'PUT', {'isRecommended': True, 'comment': 'Tilvalgt igen'})
    request('/api/system-threats/' + str(threat['id']), 'PUT', threat_data)
    assert next(c for c in request('/api/threat-controls') if
                c['systemThreatId'] == threat['id'] and c['cisControlId'] == 7)['comment'] == 'Tilvalgt igen'
    before = next(p for p in request('/api/cis-priorities') if p['control']['number'] == 7)
    request(control_path, 'PUT', {'isRecommended': False})
    after = next(p for p in request('/api/cis-priorities') if p['control']['number'] == 7)
    assert before['weightedRiskScore'] - after['weightedRiskScore'] == 7.5
    priorities = request('/api/cis-priorities')
    assert abs(sum(p['share'] for p in priorities) - 1) < 0.0001
    for blast, likelihood, score, level in [(1, 5, 5, 'Low'), (1.5, 4, 6, 'Medium'),
                                           (2.5, 5, 12.5, 'High'), (4, 5, 20, 'Critical'),
                                           (5, 5, 25, 'Critical')]:
        request('/api/system-assets/' + str(system['id']), 'PUT', {
            'name': 'Smoke WEB01', 'assetType': 'Server', 'blastRadius': blast,
            'blastRadiusReason': 'Faglig vurdering'})
        threat_data['likelihood'] = likelihood
        request('/api/system-threats/' + str(threat['id']), 'PUT', threat_data)
        risk = request(risk_path)
        assert (risk['riskScore'], risk['riskLevel']) == (score, level)
    threat_data['attackTechniqueId'] = next(t['id'] for t in request('/api/attack-techniques')
                                          if t['techniqueId'] == 'T1078')
    request('/api/system-threats/' + str(threat['id']), 'PUT', threat_data)
    assert {c['number'] for c in request(risk_path)['cisControls']} == {5, 6, 8}
    request('/api/system-threats', 'POST', dict(threat_data, likelihood=6), 400)
    request('/api/system-threats', 'POST', dict(threat_data, systemAssetId=-1), 400)
    request('/api/asset-mappings/' + str(mapping['id']), 'DELETE', status=204)
    request('/api/system-access/' + str(access['id']), 'DELETE', status=204)
    request('/api/system-threats/' + str(threat['id']), 'DELETE', status=204)
    request(risk_path, status=404)
    assert not any(c['systemThreatId'] == threat['id'] for c in request('/api/threat-controls'))
    request('/api/business-assets/' + str(assets[0]['id']), 'DELETE', status=204)
    print('PASS: 10 pages, static assets, catalogs, CRUD, all CIA combinations, validation,')
    print('      unique process consequences, fractional risks, thresholds, controls, priorities and cascades.')
finally:
    for identity in system_ids:
        request('/api/system-assets/' + str(identity), 'DELETE', status=204)
    for identity in process_ids:
        request('/api/business-processes/' + str(identity), 'DELETE', status=204)
