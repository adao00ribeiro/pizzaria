import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { NeighborhoodDto, NeighborhoodInputDto } from '../../core/models/neighborhood.models';
import { NeighborhoodsService } from './neighborhoods.service';

describe('NeighborhoodsService', () => {
  let service: NeighborhoodsService;
  let httpMock: HttpTestingController;
  const baseUrl = `${environment.apiBaseUrl}/neighborhoods`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        NeighborhoodsService,
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });

    service = TestBed.inject(NeighborhoodsService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('list() should request all neighborhoods', () => {
    const mockList: NeighborhoodDto[] = [
      { id: '1', name: 'Centro', region: 'Center', isActive: true },
      { id: '2', name: 'Santana', region: 'North', isActive: true },
    ];

    service.list().subscribe((res) => {
      expect(res.length).toBe(2);
      expect(res[0].name).toBe('Centro');
    });

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('GET');
    req.flush(mockList);
  });

  it('create() should post new neighborhood', () => {
    const input: NeighborhoodInputDto = {
      name: 'Vila Mariana',
      region: 'South',
      isActive: true,
    };
    const created: NeighborhoodDto = {
      id: '3',
      name: input.name,
      region: input.region,
      isActive: input.isActive ?? true,
    };

    service.create(input).subscribe((res) => {
      expect(res.id).toBe('3');
      expect(res.name).toBe('Vila Mariana');
    });

    const req = httpMock.expectOne(baseUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(input);
    req.flush(created);
  });

  it('update() should put changes by id', () => {
    const input: NeighborhoodInputDto = {
      name: 'Vila Mariana Alterada',
      region: 'South',
      isActive: false,
    };
    const updated: NeighborhoodDto = {
      id: '3',
      name: input.name,
      region: input.region,
      isActive: input.isActive ?? true,
    };

    service.update('3', input).subscribe((res) => {
      expect(res.name).toBe('Vila Mariana Alterada');
      expect(res.isActive).toBeFalse();
    });

    const req = httpMock.expectOne(`${baseUrl}/3`);
    expect(req.request.method).toBe('PUT');
    req.flush(updated);
  });

  it('delete() should delete by id', () => {
    service.delete('3').subscribe();

    const req = httpMock.expectOne(`${baseUrl}/3`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
