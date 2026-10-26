import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';

import { ChartSpec } from '../../core/models/ai.models';
import { ChartBlockComponent } from './chart-block.component';

describe('ChartBlockComponent', () => {
  let component: ChartBlockComponent;
  let fixture: ComponentFixture<ChartBlockComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ChartBlockComponent],
      providers: [provideCharts(withDefaultRegisterables())],
    }).compileComponents();

    fixture = TestBed.createComponent(ChartBlockComponent);
    component = fixture.componentInstance;
  });

  it('should create component', () => {
    const spec: ChartSpec = {
      kind: 'bar',
      title: 'Vendas por Região',
      labels: ['Centro', 'Norte'],
      series: [{ name: 'Pedidos', data: [15, 20] }],
      valueFormat: 'number',
    };

    fixture.componentRef.setInput('spec', spec);
    fixture.detectChanges();

    expect(component).toBeTruthy();
    expect(component.chartType()).toBe('bar');
    expect(component.hasData()).toBeTrue();
  });

  it('should detect circular chart types', () => {
    const spec: ChartSpec = {
      kind: 'pie',
      title: 'Distribuição',
      labels: ['Centro', 'Sul'],
      series: [{ name: 'Regiões', data: [10, 30] }],
      valueFormat: 'percent',
    };

    fixture.componentRef.setInput('spec', spec);
    fixture.detectChanges();

    expect(component.isCircular()).toBeTrue();
    expect(component.chartType()).toBe('pie');
  });

  it('should handle empty data gracefully', () => {
    const spec: ChartSpec = {
      kind: 'bar',
      title: 'Sem Dados',
      labels: [],
      series: [],
      valueFormat: 'number',
    };

    fixture.componentRef.setInput('spec', spec);
    fixture.detectChanges();

    expect(component.hasData()).toBeFalse();
  });
});
