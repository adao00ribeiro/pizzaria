import {
  AbstractControl,
  AbstractControlOptions,
  FormControl,
  FormGroup,
  ValidatorFn,
} from '@angular/forms';

type ValidatorsOrOptions =
  | ValidatorFn
  | ValidatorFn[]
  | AbstractControlOptions
  | null
  | undefined;

function toOptions(validatorsOrOpts: ValidatorsOrOptions): AbstractControlOptions & {
  nonNullable: true;
} {
  if (Array.isArray(validatorsOrOpts)) {
    return { validators: validatorsOrOpts, nonNullable: true };
  }
  if (typeof validatorsOrOpts === 'function') {
    return { validators: validatorsOrOpts, nonNullable: true };
  }
  return { ...(validatorsOrOpts ?? {}), nonNullable: true };
}

function nonNullableControl(
  value: unknown,
  validatorsOrOpts?: ValidatorsOrOptions,
): FormControl<unknown> {
  return new FormControl(value, toOptions(validatorsOrOpts));
}

/**
 * Alias compatível com `new FormControl(v, { nonNullable: true })`,
 * usado para manter os formulários reativos não-anuláveis.
 */
export interface NonNullableFormControl<T = unknown> extends FormControl<T> {}

export const NonNullableFormControl: new <T>(
  value: T,
  validatorsOrOpts?: ValidatorsOrOptions,
) => FormControl<T> = nonNullableControl as unknown as new <T>(
  value: T,
  validatorsOrOpts?: ValidatorsOrOptions,
) => FormControl<T>;

/**
 * Alias compatível com `new FormGroup({ ...controles })` — os controles já
 * instanciados (NonNullableFormControl) permanecem não-anuláveis.
 */
export interface NonNullableFormGroup<
  T extends { [K in keyof T]: AbstractControl<any, any> },
> extends FormGroup<T> {}

export const NonNullableFormGroup: new <
  T extends { [K in keyof T]: AbstractControl<any, any> },
>(controls: T) => FormGroup<T> = FormGroup as unknown as new <
  T extends { [K in keyof T]: AbstractControl<any, any> },
>(controls: T) => FormGroup<T>;
